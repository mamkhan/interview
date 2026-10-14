using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The builder's MeterConfigurationChangeReceived: an MM332 for 10012345678 effective 2026-09-21 stating MCC01, 01, DG1, E,
// NRN 000000123456, the removed meter's 24H actual 3842.5173 from 2026-07-16 and the new meter's 24H estimate 3910.0
// from 2026-09-21, for a supply point Store has not seen; today is 2026-10-13. Each factor is history at its own date,
// and today's 24H row, 2026-09-21's, holds both.
public class WhenAMeterConfigurationChangeArrives
{
    private readonly StoreRig _rig = new();

    public WhenAMeterConfigurationChangeArrives()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void CreatesTheSupplyPointWithItsMarket()
    {
        _rig.Repository.MarketOf("10012345678").Should().Be("roi-esb");
    }

    [Fact]
    public void StoresTheMeterPointFromTheEffectiveDateWithTheMessagesReference()
    {
        var stated = new Dated<MeterPointValues>(new DateOnly(2026, 9, 21), new MeterPointValues("MCC01", "01", "DG1", "E", "000000123456"));

        _rig.Repository.MeterPoint("10012345678").History.Should().Equal(stated);
        _rig.Repository.MeterPoint("10012345678").Current.Should().Equal(stated);
    }

    [Fact]
    public void StoresEachUsageFactorFromItsOwnEffectiveDate()
    {
        _rig.Repository.UsageFactor("10012345678", "24H").History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 7, 16), new UsageFactorValues(null, 3842.5173m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 21), new UsageFactorValues(3910.0m, null)));
        _rig.Repository.UsageFactor("10012345678", "24H").Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 21), new UsageFactorValues(3910.0m, 3842.5173m)));
    }

    [Fact]
    public void CommitsEveryRowOnceWithTheInboxMarker()
    {
        _rig.UnitOfWork.Begun.Should().Be(1);
        _rig.UnitOfWork.Commits.Should().Be(1);
    }

    [Fact]
    public void KeysTheInboxMarkerOnTheSubscriptionAndTheEvent()
    {
        _rig.UnitOfWork.Processed.Should().Equal(("metr.meter-configuration-change-received", "9a3c5e71-2b4d-5f6a-8c9e-0d1f2a3b4c5d"));
    }
}
