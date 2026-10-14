using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The profile table on this class's database. Each fact uses its own supply point, because the facts share
// one database.
public sealed class WhenStoringASupplyPointProfile(WhenStoringASupplyPointProfile.Database database)
    : IClassFixture<WhenStoringASupplyPointProfile.Database>
{
    // .123 is a value legacy DATETIME cannot hold; equality proves the parameter was typed.
    private static readonly DateTimeOffset UpdatedAt = new(2026, 9, 29, 10, 0, 0, 123, TimeSpan.Zero);

    private readonly string _connectionString = database.ConnectionString;
    private readonly SupplyPointProfileRepository _repository = new(database.ConnectionString);

    [Fact]
    public async Task FindsNoProfileForAnUnknownSupplyPoint() =>
        (await _repository.FindAsync("10000000001", TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task FindsTheConfigurationItSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000002", "roi-esb");
        profile.Apply(new MeterConfigurationStatementBuilder().Build());

        await _repository.SaveConfigurationAsync(profile, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000002", ct);
        found!.Market.Should().Be("roi-esb");
        found.Configuration.Should().Be(new MeterConfigurationStatementBuilder().Build());
    }

    [Fact]
    public async Task FindsAConfigurationWhoseCodesNoMessageHasStatedYet()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000008", "roi-esb");
        var codeless = new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode(null)
            .WithLoadProfileCode(null)
            .WithDuosGroup(null)
            .WithMeterPointStatusCode(null)
            .Build();
        profile.Apply(codeless);

        await _repository.SaveConfigurationAsync(profile, UpdatedAt, ct);

        (await _repository.FindAsync("10000000008", ct))!.Configuration.Should().Be(codeless);
    }

    [Fact]
    public async Task FindsTheUsageFactorsItSavedToTheLastDecimal()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000003", "roi-esb");
        profile.Apply(
        [
            new UsageFactorStatementBuilder().Actual(3842.5173012345m).EffectiveFrom(new DateOnly(2026, 7, 16)).Build(),
            new UsageFactorStatementBuilder().ForTimeslot("01P").Estimated(0.0000000001m).Build(),
        ]);

        await _repository.SaveUsageFactorsAsync(profile, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000003", ct);
        found!.UsageFactors.Values.Should().BeEquivalentTo(
        [
            new UsageFactorStatementBuilder().Actual(3842.5173012345m).EffectiveFrom(new DateOnly(2026, 7, 16)).Build(),
            new UsageFactorStatementBuilder().ForTimeslot("01P").Estimated(0.0000000001m).Build(),
        ]);
        found.Configuration.Should().BeNull();
    }

    [Fact]
    public async Task SavingTheUsageFactorsLeavesTheConfigurationAsItWas()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000004", "roi-esb");
        profile.Apply(new MeterConfigurationStatementBuilder().Build());
        await _repository.SaveConfigurationAsync(profile, UpdatedAt, ct);

        var factorsOnly = SupplyPointProfile.New("10000000004", "roi-esb");
        factorsOnly.Apply([new UsageFactorStatementBuilder().Build()]);
        await _repository.SaveUsageFactorsAsync(factorsOnly, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000004", ct);
        found!.Configuration.Should().Be(new MeterConfigurationStatementBuilder().Build());
        found.UsageFactors.Values.Should().Equal(new UsageFactorStatementBuilder().Build());
    }

    [Fact]
    public async Task SavingTheConfigurationLeavesTheUsageFactorsAsTheyWere()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000005", "roi-esb");
        profile.Apply([new UsageFactorStatementBuilder().Build()]);
        await _repository.SaveUsageFactorsAsync(profile, UpdatedAt, ct);

        var configurationOnly = SupplyPointProfile.New("10000000005", "roi-esb");
        configurationOnly.Apply(new MeterConfigurationStatementBuilder().Build());
        await _repository.SaveConfigurationAsync(configurationOnly, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000005", ct);
        found!.UsageFactors.Values.Should().Equal(new UsageFactorStatementBuilder().Build());
        found.Configuration.Should().Be(new MeterConfigurationStatementBuilder().Build());
    }

    [Fact]
    public async Task ReplacesAFactorOnALaterSave()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000006", "roi-esb");
        profile.Apply([new UsageFactorStatementBuilder().Build()]);
        await _repository.SaveUsageFactorsAsync(profile, UpdatedAt, ct);

        var stored = await _repository.FindAsync("10000000006", ct);
        stored!.Apply([new UsageFactorStatementBuilder().Estimated(4100.0m).EffectiveFrom(new DateOnly(2026, 10, 1)).Build()]);
        await _repository.SaveUsageFactorsAsync(stored, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000006", ct);
        found!.UsageFactors.Values.Should().Equal(
            new UsageFactorStatementBuilder().Estimated(4100.0m).EffectiveFrom(new DateOnly(2026, 10, 1)).Build());
        (await CountAsync("10000000006")).Should().Be(1);
    }

    [Fact]
    public async Task StoresTheUpdateTimeToTheMillisecond()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000007", "roi-esb");
        profile.Apply(new MeterConfigurationStatementBuilder().Build());

        await _repository.SaveConfigurationAsync(profile, UpdatedAt, ct);

        await using var connection = new SqlConnection(_connectionString);
        (await connection.ExecuteScalarAsync<DateTimeOffset>(new CommandDefinition(
            "SELECT UpdatedAt FROM dbo.SupplyPointProfile WHERE SupplyPointId = '10000000007'", cancellationToken: ct)))
            .Should().Be(UpdatedAt);
    }

    [Fact]
    public async Task LetsBothGroupsCreateANewRowAtTheSameMoment()
    {
        var ct = TestContext.Current.CancellationToken;
        var supplyPoints = Enumerable.Range(0, 40).Select(i => $"2000000{i:D4}").ToArray();

        await Task.WhenAll(supplyPoints.SelectMany(supplyPointId =>
        {
            var configuration = SupplyPointProfile.New(supplyPointId, "roi-esb");
            configuration.Apply(new MeterConfigurationStatementBuilder().Build());
            var factors = SupplyPointProfile.New(supplyPointId, "roi-esb");
            factors.Apply([new UsageFactorStatementBuilder().Build()]);
            return new[]
            {
                Task.Run(() => _repository.SaveConfigurationAsync(configuration, UpdatedAt, ct), ct),
                Task.Run(() => _repository.SaveUsageFactorsAsync(factors, UpdatedAt, ct), ct),
            };
        }));

        foreach (var supplyPointId in supplyPoints)
        {
            var found = await _repository.FindAsync(supplyPointId, ct);
            found!.Configuration.Should().NotBeNull();
            found.UsageFactors.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task FindsTheTariffItSavedToTheMillisecond()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000009", "roi-esb");
        profile.Apply(new TariffStatementBuilder().ReadAt(new DateTimeOffset(2026, 10, 7, 4, 0, 0, 123, TimeSpan.Zero)).Build());

        await _repository.SaveTariffAsync(profile, UpdatedAt, ct);

        (await _repository.FindAsync("10000000009", ct))!.Tariff.Should().Be(
            TariffStatement.Of(true, "TwentyFourHour", "LP1", new DateTimeOffset(2026, 10, 7, 4, 0, 0, 123, TimeSpan.Zero),
                "v1-reader/tariff-values"));
    }

    [Fact]
    public async Task FindsATariffThatStatesNoValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000010", "roi-esb");
        profile.Apply(new TariffStatementBuilder().WithNoTariff().Build());

        await _repository.SaveTariffAsync(profile, UpdatedAt, ct);

        (await _repository.FindAsync("10000000010", ct))!.Tariff.Should().Be(new TariffStatementBuilder().WithNoTariff().Build());
    }

    [Fact]
    public async Task FindsTheSmartMeterFlagItSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000011", "roi-esb");
        profile.Apply(new SmartMeterFlagStatementBuilder().NotSmart().FromSource("v1-reader:2026-10-07:0").Build());

        await _repository.SaveSmartMeterFlagAsync(profile, UpdatedAt, ct);

        (await _repository.FindAsync("10000000011", ct))!.SmartMeterFlag.Should().Be(
            new SmartMeterFlagStatement(false, new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero), "v1-reader:2026-10-07:0"));
    }

    [Fact]
    public async Task SavingTheTariffOrTheFlagLeavesEveryOtherGroupAsItWas()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000012", "roi-esb");
        profile.Apply(new MeterConfigurationStatementBuilder().Build());
        profile.Apply([new UsageFactorStatementBuilder().Build()]);
        await _repository.SaveConfigurationAsync(profile, UpdatedAt, ct);
        await _repository.SaveUsageFactorsAsync(profile, UpdatedAt, ct);

        var tariffOnly = SupplyPointProfile.New("10000000012", "roi-esb");
        tariffOnly.Apply(new TariffStatementBuilder().Build());
        await _repository.SaveTariffAsync(tariffOnly, UpdatedAt, ct);
        var flagOnly = SupplyPointProfile.New("10000000012", "roi-esb");
        flagOnly.Apply(new SmartMeterFlagStatementBuilder().Build());
        await _repository.SaveSmartMeterFlagAsync(flagOnly, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000012", ct);
        found!.Configuration.Should().Be(new MeterConfigurationStatementBuilder().Build());
        found.UsageFactors.Values.Should().Equal(new UsageFactorStatementBuilder().Build());
        found.Tariff.Should().Be(new TariffStatementBuilder().Build());
        found.SmartMeterFlag.Should().Be(new SmartMeterFlagStatementBuilder().Build());
    }

    [Fact]
    public async Task SavingTheConfigurationLeavesTheTariffAndTheFlagAsTheyWere()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000013", "roi-esb");
        profile.Apply(new TariffStatementBuilder().Build());
        profile.Apply(new SmartMeterFlagStatementBuilder().Build());
        await _repository.SaveTariffAsync(profile, UpdatedAt, ct);
        await _repository.SaveSmartMeterFlagAsync(profile, UpdatedAt, ct);

        var configurationOnly = SupplyPointProfile.New("10000000013", "roi-esb");
        configurationOnly.Apply(new MeterConfigurationStatementBuilder().Build());
        await _repository.SaveConfigurationAsync(configurationOnly, UpdatedAt, ct);

        var found = await _repository.FindAsync("10000000013", ct);
        found!.Tariff.Should().Be(new TariffStatementBuilder().Build());
        found.SmartMeterFlag.Should().Be(new SmartMeterFlagStatementBuilder().Build());
    }

    private async Task<int> CountAsync(string supplyPointId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.SupplyPointProfile WHERE SupplyPointId = @supplyPointId",
            new { supplyPointId }, cancellationToken: TestContext.Current.CancellationToken));
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenStoringASupplyPointProfile), ConsumptionSchema.ApplyConsumptionSchemaAsync);
}
