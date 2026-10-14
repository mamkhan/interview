namespace MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

// For 10012345678: the builder's MM332 effective 2026-09-21 (DG1, the 24H actual from 2026-07-16 and estimate from
// 2026-09-21) arrives, is delivered a second time, and then an MM301 effective 2026-08-01 stating only DG2 arrives late.
// The session on the MPRN keeps the three in order, so once the backdated history row is there the redelivery has been
// handled too. Then the real MM320 sample arrives for 10900000320. The host's clock is the system's, after every date.
public sealed class MeterConfigurationChangesArrivedOnTheBusScenario : IAsyncLifetime
{
    private StoreOnTheBus _bus = null!;

    public IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)> FirstMeterPoint { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)> UsageFactorHistory { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, decimal? Estimated, decimal? Actual)> UsageFactorCurrent { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)> MeterPointHistoryAfterAll { get; private set; } = [];

    public IReadOnlyList<(DateTime EffectiveDate, string? DuosGroup)> MeterPointCurrentAfterAll { get; private set; } = [];

    public IReadOnlyList<(string SerialNumber, DateTime EffectiveDate, string? Category)> Meters { get; private set; } = [];

    public IReadOnlyList<(string SerialNumber, string Key, DateTime EffectiveDate, decimal? Read)> Registers { get; private set; } = [];

    public int InboxMarkers { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _bus = await StoreOnTheBus.StartAsync(nameof(WhenMeterConfigurationChangesArriveOnTheBus), ct);

        var first = new MeterConfigurationChangeReceivedBuilder().Build();
        await _bus.SendAsync(first, ct);
        FirstMeterPoint = await _bus.PollMeterPointAsync("MeterPoint", "10012345678", rows: 1, ct);
        UsageFactorHistory = await _bus.PollUsageFactorAsync("UsageFactorHistory", "10012345678", rows: 2, ct);
        UsageFactorCurrent = await _bus.PollUsageFactorAsync("UsageFactor", "10012345678", rows: 1, ct);

        await _bus.SendAsync(first, ct);
        await _bus.SendAsync(new MeterConfigurationChangeReceivedBuilder()
            .EffectiveFrom(new DateOnly(2026, 8, 1))
            .WithCodes(null, null, "DG2", null)
            .WithoutUsageFactors()
            .WithEventId(Guid.Parse("8c2b1f1e-1d5e-4e2a-9f0a-0a0000000301"))
            .Build(), ct);
        MeterPointHistoryAfterAll = await _bus.PollMeterPointAsync("MeterPointHistory", "10012345678", rows: 2, ct);
        MeterPointCurrentAfterAll = await _bus.PollMeterPointAsync("MeterPoint", "10012345678", rows: 1, ct);

        await _bus.SendAsync(new MeterConfigurationChangeReceivedBuilder().AsTheRealMm320States().Build(), ct);
        Meters = await _bus.PollMeterAsync("10900000320", rows: 1, ct);
        Registers = await _bus.PollRegisterAsync("10900000320", rows: 3, ct);
        InboxMarkers = await _bus.InboxMarkersAsync(ct);
    }

    public async ValueTask DisposeAsync() => await _bus.DisposeAsync();
}
