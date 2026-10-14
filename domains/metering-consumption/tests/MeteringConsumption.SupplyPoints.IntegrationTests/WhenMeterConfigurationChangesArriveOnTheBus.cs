using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The Store role's one consumer on its production subscription: each message lands whole, a redelivery adds nothing, and
// a message effective earlier arriving later is history at its own date.
public sealed class WhenMeterConfigurationChangesArriveOnTheBus(MeterConfigurationChangesArrivedOnTheBusScenario arrived)
    : IClassFixture<MeterConfigurationChangesArrivedOnTheBusScenario>
{
    [Fact]
    public void LandsTodaysMeterPointRowFromTheEffectiveDate()
    {
        arrived.FirstMeterPoint.Should().Equal((new DateTime(2026, 9, 21), "DG1"));
    }

    [Fact]
    public void LandsAHistoryRowPerUsageFactorAndTodaysRowWithBoth()
    {
        arrived.UsageFactorHistory.Should().Equal(
            (new DateTime(2026, 7, 16), (decimal?)null, (decimal?)3842.5173m),
            (new DateTime(2026, 9, 21), 3910.0m, null));
        arrived.UsageFactorCurrent.Should().Equal((new DateTime(2026, 9, 21), (decimal?)3910.0m, (decimal?)3842.5173m));
    }

    // The 2026-09-21 message stated DG1 itself, so today's row keeps it.
    [Fact]
    public void KeepsTheLateMessageAsHistoryAtItsOwnDate()
    {
        arrived.MeterPointHistoryAfterAll.Should().Equal((new DateTime(2026, 8, 1), "DG2"), (new DateTime(2026, 9, 21), "DG1"));
        arrived.MeterPointCurrentAfterAll.Should().Equal((new DateTime(2026, 9, 21), "DG1"));
    }

    [Fact]
    public void LandsTheMeterAndEveryRegisterOfAWholeFile()
    {
        arrived.Meters.Should().Equal(("90000320", new DateTime(2026, 9, 30), "RM107"));
        arrived.Registers.Should().Equal(
            ("90000320", "001", new DateTime(2026, 9, 30), (decimal?)0m),
            ("90000320", "002", new DateTime(2026, 9, 30), (decimal?)0m),
            ("90000320", "003", new DateTime(2026, 9, 30), (decimal?)0m));
    }

    [Fact]
    public void HandlesTheRedeliveryOnlyOnce()
    {
        arrived.InboxMarkers.Should().Be(3);
    }
}
