using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The real MM331 sample as the builder states it: meter 90003311 put in place at 10900000331 on 2026-09-16 (category
// RM106, location 07, exchanged reference 000000000090003311) with channel 70 (KWT, every 30 minutes) and channel 74
// (KWH, every 24), which have no sequence number and no read, for a supply point Store has not seen; today is 2026-10-13.
public class WhenANewSmartMeterIsStated
{
    private readonly StoreRig _rig = new();

    public WhenANewSmartMeterIsStated()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().AsTheRealMm331States().Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresTheMeterInPlaceFromTheDate() =>
        _rig.Repository.Meter("10900000331", "90003311").Current.Should().Equal(new Dated<MeterDetails>(
            new DateOnly(2026, 9, 16), new MeterDetails("RM106", "07", "000000000090003311")));

    [Fact]
    public void StoresTheChannelUnderItsRegisterTypeAndUnit() =>
        _rig.Repository.Register("10900000331", "90003311", "70:KWT").Current.Should().ContainSingle()
            .Which.Values.Should().Be(new RegisterValues(
                new RegisterDetails { RegisterTypeCode = "70", UomCode = "KWT", MeteringInterval = 30 }, null));

    [Fact]
    public void StoresTheOtherChannelUnderItsOwnKey() =>
        _rig.Repository.Register("10900000331", "90003311", "74:KWH").Current.Should().ContainSingle()
            .Which.Values.Details.MeteringInterval.Should().Be(24);

    [Fact]
    public void CreatesTheSupplyPointAndCommitsOnce()
    {
        _rig.Repository.MarketOf("10900000331").Should().Be("roi-esb");
        _rig.UnitOfWork.Commits.Should().Be(1);
    }
}
