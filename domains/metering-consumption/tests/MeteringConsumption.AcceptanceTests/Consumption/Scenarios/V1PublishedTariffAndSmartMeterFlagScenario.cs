namespace MeteringConsumption.AcceptanceTests.Consumption.Scenarios;

// Publishes what the V1 reader publishes each night for a new supply point, TariffResolved on Growth's topic and a
// meter configuration stating only the smart meter flag, and reads back the row once both groups are there.
public sealed class V1PublishedTariffAndSmartMeterFlagScenario : IAsyncLifetime
{
    private const string RowSql = """
        SELECT IsTou, TariffCalcType, EffectiveLoadProfileCode, IsNsmp
        FROM dbo.SupplyPointProfile
        WHERE SupplyPointId = @Mprn;
        """;

    public V1Row Row { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = TargetEnvironment.Discover();
        var mprn = ProfileEvents.NewMprn();

        await ProfileEvents.PublishAsync(environment, new TariffResolvedBuilder().ForMprn(mprn).Build(), ct);
        await ProfileEvents.PublishAsync(environment,
            new MeterConfigurationReceivedBuilder().ForMprn(mprn).AsTheV1ReaderStatesIt(isNsmp: true).Build(), ct);

        Row = await ProfileEvents.WaitForTheRowAsync<V1Row>(environment, RowSql, mprn,
            row => row is { IsTou: not null, IsNsmp: not null }, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public sealed record V1Row
    {
        public bool? IsTou { get; init; }

        public string? TariffCalcType { get; init; }

        public string? EffectiveLoadProfileCode { get; init; }

        public bool? IsNsmp { get; init; }
    }
}
