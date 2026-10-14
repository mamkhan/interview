using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

// For 10012345678: the builder's load from the run of 2026-10-12 arrives, then the run of 2026-10-13 loads it again with
// DG2 from 2026-09-15 and only the 24H estimate 4000.0 from 2026-09-15. Each run's start is its events' AsOf, so it is
// the day the reload is recorded on. The second run waits for the first one's rows, so the two never meet without
// sessions.
public sealed class SupplyPointsLoadedFromV1ArrivedOnTheBusScenario : IAsyncLifetime
{
    private ReloadOnTheBus _bus = null!;

    public IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)> First { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)> FirstUsageFactorHistory { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)> FirstUsageFactorCurrent { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)> Second { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)> SecondUsageFactorHistory { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _bus = await ReloadOnTheBus.StartAsync(nameof(WhenSupplyPointsLoadedFromV1ArriveOnTheBus), ct);

        await _bus.SendAsync(new SupplyPointLoadedFromV1Builder().Build(), ct);
        First = await _bus.PollMeterPointAsync("10012345678", rows => rows.Count == 1, ct);
        FirstUsageFactorHistory = await _bus.ReadUsageFactorsAsync("UsageFactorHistory", "10012345678", ct);
        FirstUsageFactorCurrent = await _bus.ReadUsageFactorsAsync("UsageFactor", "10012345678", ct);

        await _bus.SendAsync(new SupplyPointLoadedFromV1Builder()
            .WithEventId(Guid.Parse("6b1e3c9d-8a2f-5d4b-9c7e-1f3a5b7d9e20"))
            .StartedAt(new DateTimeOffset(2026, 10, 13, 5, 0, 0, TimeSpan.Zero))
            .WithCodes(new DateOnly(2026, 9, 15), "MCC01", "01", "DG2")
            .WithUsageFactors(new LoadedUsageFactor("24H", new DateOnly(2026, 9, 15), 4000.0m, null))
            .Build(), ct);
        Second = await _bus.PollMeterPointAsync("10012345678", rows => rows.Count == 1 && rows[0].DuosGroup == "DG2", ct);
        SecondUsageFactorHistory = await _bus.ReadUsageFactorsAsync("UsageFactorHistory", "10012345678", ct);
    }

    public async ValueTask DisposeAsync() => await _bus.DisposeAsync();
}
