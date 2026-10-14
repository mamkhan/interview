using AwesomeAssertions;
using BuildingBlocks.Seedwork;
using MeteringConsumption.SupplyPoints.Application.Features.Store;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// A message with neither an effective nor a read date has no date to take effect from. It is refused and its work rolls
// back uncommitted, so the consumer dead-letters it on first delivery and no supply point is created. The builder's usage factors
// carry dates of their own, 2026-07-16 and 2026-09-21, and are not stored either: a message is stored whole or not at all.
public class WhenAMeterConfigurationChangeCannotBeStored
{
    private readonly StoreRig _rig = new();

    [Fact]
    public async Task RefusesTheWholeMessageWhenItHasNoDate()
    {
        var store = () => _rig.Handler<MeterConfigurationChangeReceivedEventHandler>()
            .HandleAsync(new MeterConfigurationChangeReceivedBuilder().WithNoDates().Build(), CancellationToken.None);

        await store.Should().ThrowAsync<DomainException>();
        _rig.UnitOfWork.Commits.Should().Be(0);
        _rig.Repository.MarketOf("10012345678").Should().BeNull();
        _rig.Repository.UsageFactor("10012345678", "24H").History.Should().BeEmpty();
    }
}
