using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// An MM320 states a read date, 2026-08-14, and no effective date: the meter point takes effect on the read date. Its
// load profile arrives blank, which ESB means as not stated, so it stays empty on a supply point with nothing before it.
public class WhenAMeterConfigurationChangeStatesOnlyAReadDate
{
    private readonly StoreRig _rig = new();

    public WhenAMeterConfigurationChangeStatesOnlyAReadDate()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().ReadOn(new DateOnly(2026, 8, 14)).WithLoadProfileCode(" ").Build(),
                CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void TakesEffectOnTheReadDate()
    {
        _rig.Repository.MeterPoint("10012345678").History.Single().EffectiveDate.Should().Be(new DateOnly(2026, 8, 14));
    }

    [Fact]
    public void TakesABlankCodeAsNotStated()
    {
        _rig.Repository.MeterPoint("10012345678").History.Single().Values.LoadProfileCode.Should().BeNull();
    }
}
