using AwesomeAssertions;
using BuildingBlocks.Seedwork;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// A load with a blank MPRN names no supply point. It is refused before anything is stored, so the consumer dead-letters
// it on first delivery.
public class WhenALoadCannotBeStored
{
    private readonly ReloadRig _rig = new();

    [Fact]
    public async Task RefusesALoadWithNoMprn()
    {
        var load = () => _rig.Handler<SupplyPointLoadedFromV1EventHandler>()
            .HandleAsync(new SupplyPointLoadedFromV1Builder().ForMprn(" ").Build(), CancellationToken.None);

        await load.Should().ThrowAsync<DomainException>();
        _rig.UnitOfWork.Commits.Should().Be(0);
    }
}
