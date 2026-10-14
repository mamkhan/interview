using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Consumption.IntegrationTests.Scenarios;

// One supply point is sent what each of the Profile role's consumers reads: Market Messaging's meter configuration and
// usage factors, then the V1 reader's tariff values on Growth's topic and its smart meter flag behind the market's
// configuration on the same session.
public sealed class ProfileStatementsArrivedOnTheBusScenario : IAsyncLifetime
{
    private const string Mprn = "10000000401";

    private ProfileOnTheBus _profile = null!;

    public SupplyPointProfile? Profile { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _profile = await ProfileOnTheBus.StartAsync(nameof(WhenProfileStatementsArriveOnTheBus), ct);

        await _profile.SendAsync(new MeterConfigurationReceivedBuilder().ForMprn(Mprn).Build(), ct);
        await _profile.SendAsync(new MeterConfigurationReceivedBuilder().ForMprn(Mprn).AsTheV1ReaderStatesIt(isNsmp: true).Build(), ct);
        await _profile.SendAsync(new UsageFactorsReceivedBuilder().ForMprn(Mprn).WithFactors(
            new UsageFactor("24H", new DateOnly(2026, 7, 16), EstimatedUsageFactor: null, ActualUsageFactor: 3842.51730123m),
            new UsageFactor("00N", new DateOnly(2026, 9, 21), EstimatedUsageFactor: 1250.5m, ActualUsageFactor: null)).Build(), ct);
        await _profile.SendAsync(new TariffResolvedBuilder().ForMprn(Mprn).Build(), ct);

        Profile = await _profile.PollAsync(Mprn, profile => profile is
        {
            Configuration: not null, SmartMeterFlag: not null, Tariff: not null, UsageFactors.Count: 2,
        }, ct);
    }

    public ValueTask DisposeAsync() => _profile.DisposeAsync();
}
