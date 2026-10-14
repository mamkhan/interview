using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The reference-data database as another process fills it: LP1 on 2026-05-01 and 05-02, hour h holding h/1000 on the
// whole-day curve, and DG1's 2026 averages. Values are written with plain SQL, as that process would.
public sealed class WhenReadingMarketReferenceData : IAsyncLifetime
{
    private MarketReferenceDataRepository _reference = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenReadingMarketReferenceData),
            new SuiteResources { Migrate = ReferenceDataSchema.ApplyReferenceDataSchemaAsync }, ct);
        _reference = new MarketReferenceDataRepository(scope.SqlConnectionString!);

        await using var connection = new SqlConnection(scope.SqlConnectionString);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO dbo.LoadProfileHourlyValue (LoadProfileCode, ValueDate, [Hour], H24, D00, N00)
            SELECT 'LP1', d.ValueDate, h.[Hour], h.[Hour] / 1000.0, NULL, 0.0001234567
            FROM (VALUES (CAST('2026-05-01' AS date)), (CAST('2026-05-02' AS date))) AS d(ValueDate)
            CROSS JOIN (SELECT TOP (24) CAST(ROW_NUMBER() OVER (ORDER BY object_id) - 1 AS tinyint) AS [Hour] FROM sys.objects) AS h;

            INSERT INTO dbo.AnnualAverageUsage (DuosGroup, [Year], Aau24H, Aau00D, Aau00N) VALUES ('DG1', 2026, 3853, 2945, 1966);
            """, cancellationToken: ct));
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReadsEveryHourOfTheDatesAskedFor()
    {
        var curve = await _reference.CurveAsync("LP1", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2),
            TestContext.Current.CancellationToken);

        curve.Sum(new DateOnly(2026, 5, 1), 2, CurveBand.Flat).Should().Be(0.552m);
        curve.Hours(new DateOnly(2026, 5, 2), 1, CurveBand.Night)!.Should().OnlyContain(value => value == 0.0001234567m);
        curve.Hours(new DateOnly(2026, 5, 1), 1, CurveBand.Day).Should().BeNull();
    }

    [Fact]
    public async Task HoldsNoHourOutsideTheDatesAskedFor()
    {
        var curve = await _reference.CurveAsync("LP1", new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 2),
            TestContext.Current.CancellationToken);

        curve.Hours(new DateOnly(2026, 5, 1), 1, CurveBand.Flat).Should().BeNull();
    }

    [Fact]
    public async Task ReadsADuosGroupsAveragesForTheYear()
    {
        var ct = TestContext.Current.CancellationToken;

        (await _reference.AnnualAverageAsync("DG1", 2026, ct)).Should().Be(new AnnualAverageUsage(3853m, 2945m, 1966m));
        (await _reference.AnnualAverageAsync("DG1", 2027, ct)).Should().BeNull();
    }
}
