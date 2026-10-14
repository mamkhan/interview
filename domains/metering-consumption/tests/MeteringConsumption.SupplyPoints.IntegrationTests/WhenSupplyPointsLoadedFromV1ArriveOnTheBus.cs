using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The Reload role's one consumer on its production subscription, without sessions: a load lands the supply point's
// codes and usage factors, and a later run's load replaces them.
public sealed class WhenSupplyPointsLoadedFromV1ArriveOnTheBus(SupplyPointsLoadedFromV1ArrivedOnTheBusScenario arrived)
    : IClassFixture<SupplyPointsLoadedFromV1ArrivedOnTheBusScenario>
{
    [Fact]
    public void LandsTodaysMeterPointRowFromV1sDate()
    {
        arrived.First.Should().Equal((new DateTime(2026, 8, 3), "DG1"));
    }

    [Fact]
    public void LandsTheEstimateAndTheLaterActual()
    {
        arrived.FirstUsageFactorHistory.Should().Equal(
            (new DateTime(2026, 8, 3), (decimal?)4127.5m, (decimal?)null),
            (new DateTime(2026, 9, 1), null, 3980.25m));
        arrived.FirstUsageFactorCurrent.Should().Equal((new DateTime(2026, 9, 1), (decimal?)4127.5m, (decimal?)3980.25m));
    }

    [Fact]
    public void ReplacesThemWhenALaterRunLoadsTheSupplyPointAgain()
    {
        arrived.Second.Should().Equal((new DateTime(2026, 9, 15), "DG2"));
        arrived.SecondUsageFactorHistory.Should().Equal((new DateTime(2026, 9, 15), (decimal?)4000.0m, (decimal?)null));
    }
}
