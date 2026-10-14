using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// The builder's load of 10012345678, a supply point no message has reached: MCC01, 01, DG1 from 2026-08-03, the 24H
// estimate 4127.5 from 2026-08-03 and the 24H actual 3980.25 from 2026-09-01; today is 2026-10-13. V1 states no meter
// point status and no networks reference number.
public class WhenASupplyPointIsFirstLoadedFromV1
{
    private readonly ReloadRig _rig = new();

    public WhenASupplyPointIsFirstLoadedFromV1()
    {
        _rig.Handler<SupplyPointLoadedFromV1EventHandler>()
            .HandleAsync(new SupplyPointLoadedFromV1Builder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void CreatesTheSupplyPointWithItsMarket()
    {
        _rig.Repository.MarketOf("10012345678").Should().Be("roi-esb");
    }

    [Fact]
    public void StoresTheCodesFromV1sDateWithNoStatusAndNoReference()
    {
        var loaded = new Dated<MeterPointValues>(new DateOnly(2026, 8, 3), new MeterPointValues("MCC01", "01", "DG1", null, null));

        _rig.Repository.MeterPoint("10012345678").History.Should().Equal(loaded);
        _rig.Repository.MeterPoint("10012345678").Current.Should().Equal(loaded);
    }

    [Fact]
    public void StoresEachUsageFactorAtItsOwnDateAndTodaysRowWithBoth()
    {
        _rig.Repository.UsageFactor("10012345678", "24H").History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 8, 3), new UsageFactorValues(4127.5m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 1), new UsageFactorValues(null, 3980.25m)));
        _rig.Repository.UsageFactor("10012345678", "24H").Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 1), new UsageFactorValues(4127.5m, 3980.25m)));
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
        _rig.UnitOfWork.Processed.Should().Equal(("metr.supply-point-loaded-from-v1", "4d2f8a61-3c7b-5e9a-8b1d-6f0e2a4c8e13"));
    }
}
