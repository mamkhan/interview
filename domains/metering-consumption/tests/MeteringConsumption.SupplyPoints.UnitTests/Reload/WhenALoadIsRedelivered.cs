using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// The builder's load delivered twice: the inbox marker of the first delivery, committed with its rows, makes the second
// a no-op.
public class WhenALoadIsRedelivered
{
    private readonly ReloadRig _rig = new();

    public WhenALoadIsRedelivered()
    {
        var handler = _rig.Handler<SupplyPointLoadedFromV1EventHandler>();
        var loaded = new SupplyPointLoadedFromV1Builder().Build();
        handler.HandleAsync(loaded, CancellationToken.None).GetAwaiter().GetResult();
        handler.HandleAsync(loaded, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void CommitsOnlyTheFirstDelivery()
    {
        _rig.UnitOfWork.Commits.Should().Be(1);
        _rig.Repository.MeterPoint("10012345678").History.Should().ContainSingle();
    }
}
