using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Store;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The same MeterConfigurationChangeReceived (event 9a3c5e71…) is delivered twice: the inbox holds its marker after the
// first commit, so the second delivery runs nothing and commits nothing.
public class WhenAMeterConfigurationChangeIsRedelivered
{
    private readonly StoreRig _rig = new();

    public WhenAMeterConfigurationChangeIsRedelivered()
    {
        var handler = _rig.Handler<MeterConfigurationChangeReceivedEventHandler>();
        handler.HandleAsync(new MeterConfigurationChangeReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
        handler.HandleAsync(new MeterConfigurationChangeReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresEveryHistoryRowOnce()
    {
        _rig.Repository.MeterPoint("10012345678").History.Should().ContainSingle();
        _rig.Repository.UsageFactor("10012345678", "24H").History.Should().HaveCount(2);
    }

    [Fact]
    public void CommitsOnlyTheFirstDelivery()
    {
        _rig.UnitOfWork.Commits.Should().Be(1);
    }
}
