using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The builder's MM332 for 10012345678 (effective 2026-09-21, the 24H actual 3842.5173 from 2026-07-16 and estimate
// 3910.0 from 2026-09-21) arrives while the host's clock reads 2026-09-01. Today's 24H row is 2026-07-16's, and current
// also holds the 2026-09-21 rows already known.
public class WhenAMeterConfigurationChangeTakesEffectAfterToday
{
    private readonly StoreRig _rig = new(today: new DateOnly(2026, 9, 1));

    public WhenAMeterConfigurationChangeTakesEffectAfterToday()
    {
        _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void KeepsTodaysRowAndTheFutureRow() =>
        _rig.Repository.UsageFactor("10012345678", "24H").Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 7, 16), new UsageFactorValues(null, 3842.5173m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 21), new UsageFactorValues(3910.0m, 3842.5173m)));
}
