using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.Consumption.IntegrationTests;

// Forecast versions on this class's database, each fact on its own supply point. Day 1 of 8.7259314999 kWh stores as
// 8.725931 and day 2 of 8.6982145 as 8.698215 (half away from zero), so the stored total is 17.424146.
public sealed class WhenStoringAForecast : IAsyncLifetime
{
    // .123 is a value legacy DATETIME cannot hold; equality proves the parameter was typed.
    private static readonly DateTimeOffset ComputedAt = new(2026, 6, 25, 2, 51, 4, 123, TimeSpan.Zero);

    private ConsumptionForecastRepository _store = null!;

    public async ValueTask InitializeAsync()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenStoringAForecast),
            new SuiteResources { Migrate = ConsumptionSchema.ApplyConsumptionSchemaAsync },
            TestContext.Current.CancellationToken);
        _store = new ConsumptionForecastRepository(scope.SqlConnectionString!);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static NewForecast Forecast(string supplyPointId, string trigger, DateTimeOffset? computedAt = null) => new()
    {
        SupplyPointId = supplyPointId,
        ComputedAtUtc = computedAt ?? ComputedAt,
        AnchorDate = new DateOnly(2026, 6, 25),
        Daily = [new ForecastedDay(new DateOnly(2026, 6, 25), 8.7259314999m), new ForecastedDay(new DateOnly(2026, 6, 26), 8.6982145m)],
        UsageFactorSource = "PUF",
        TriggerLedgerEntryId = trigger,
        Inputs = """{"usageFactorSource":"PUF"}""",
    };

    [Fact]
    public async Task StoresTheFirstVersionAsCurrentToSixDecimals()
    {
        var ct = TestContext.Current.CancellationToken;

        var stored = await _store.SaveAsync(Forecast("10000000001", "le:1"), ct);
        var found = await _store.FindByTriggerAsync("10000000001", "le:1", ct);

        stored.ForecastVersion.Should().Be(1);
        stored.ForecastId.Should().Be("10000000001:1");
        found.Should().BeEquivalentTo(stored);
        found!.IsCurrent.Should().BeTrue();
        found.ComputedAtUtc.Should().Be(ComputedAt);
        found.Daily.Should().Equal(
            new ForecastedDay(new DateOnly(2026, 6, 25), 8.725931m), new ForecastedDay(new DateOnly(2026, 6, 26), 8.698215m));
        found.TotalKwh.Should().Be(17.424146m);
        found.HorizonDays.Should().Be(2);
    }

    [Fact]
    public async Task MakesTheNextVersionCurrentAndRetiresTheLast()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.SaveAsync(Forecast("10000000002", "le:1"), ct);
        var second = await _store.SaveAsync(Forecast("10000000002", "le:2"), ct);

        second.ForecastVersion.Should().Be(2);
        (await _store.FindByTriggerAsync("10000000002", "le:1", ct))!.IsCurrent.Should().BeFalse();
        (await _store.FindByTriggerAsync("10000000002", "le:2", ct))!.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task KeepsOnlyTheDailySeriesOfASupersededVersion()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.SaveAsync(Forecast("10000000007", "le:1"), ct);
        await _store.SaveAsync(Forecast("10000000007", "le:2"), ct);

        var superseded = (await _store.FindByTriggerAsync("10000000007", "le:1", ct))!;
        superseded.Inputs.Should().BeEmpty();
        superseded.Daily.Select(day => day.Kwh).Should().Equal(8.725931m, 8.698215m);
        (await _store.FindByTriggerAsync("10000000007", "le:2", ct))!.Inputs.Should().Be("""{"usageFactorSource":"PUF"}""");
    }

    [Fact]
    public async Task RefusesASecondVersionForTheSameRecordedReading()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(Forecast("10000000003", "le:1"), ct);

        var again = () => _store.SaveAsync(Forecast("10000000003", "le:1"), ct);

        await again.Should().ThrowAsync<SqlException>();
        (await _store.FindByTriggerAsync("10000000003", "le:1", ct))!.ForecastVersion.Should().Be(1);
    }

    [Fact]
    public async Task FindsNothingForAReadingWithNoForecast() =>
        (await _store.FindByTriggerAsync("10000000004", "le:1", TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task DeletesOnlySupersededVersionsComputedBeforeTheCutOff()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(Forecast("10000000005", "le:1", ComputedAt.AddDays(-40)), ct);
        await _store.SaveAsync(Forecast("10000000005", "le:2", ComputedAt.AddDays(-35)), ct);
        await _store.SaveAsync(Forecast("10000000005", "le:3", ComputedAt.AddDays(-1)), ct);

        await _store.DeleteSupersededAsync(ComputedAt.AddDays(-30), 100, ct);

        (await _store.FindByTriggerAsync("10000000005", "le:1", ct)).Should().BeNull();
        (await _store.FindByTriggerAsync("10000000005", "le:2", ct)).Should().BeNull();
        (await _store.FindByTriggerAsync("10000000005", "le:3", ct))!.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task NeverDeletesTheCurrentVersionHoweverOld()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SaveAsync(Forecast("10000000006", "le:1", ComputedAt.AddDays(-90)), ct);

        await _store.DeleteSupersededAsync(ComputedAt, 100, ct);

        (await _store.FindByTriggerAsync("10000000006", "le:1", ct)).Should().NotBeNull();
    }
}
