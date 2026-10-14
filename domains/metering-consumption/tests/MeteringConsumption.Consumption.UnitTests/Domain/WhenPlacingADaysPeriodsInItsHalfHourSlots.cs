using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Each period's kWh is its ordinal, so a slot shows which period landed in it. In 2026 the clocks go forward on
// Sunday 29 March and back on Sunday 25 October. Totals are the sums of the ordinals: 1..46 = 1,081, 1..50 = 1,275.
public sealed class WhenPlacingADaysPeriodsInItsHalfHourSlots
{
    private static ReadDay Place(DateOnly readDate, int periods) =>
        ReadDay.FromPeriods(readDate, Enumerable.Range(1, periods).Select(period => new EntryPeriod(period, period, "VVAK")));

    [Fact]
    public void PutsEachPeriodOfAnOrdinaryDayInItsOwnSlot()
    {
        var day = Place(new DateOnly(2026, 6, 24), 48);

        day.Slots[0].Should().Be(1m);
        day.Slots[47].Should().Be(48m);
    }

    [Fact]
    public void LeavesTheMissingHourEmptyWhenTheClocksGoForward()
    {
        var day = Place(new DateOnly(2026, 3, 29), 46);

        day.Slots.Take(6).Should().Equal(1m, 2m, 0m, 0m, 3m, 4m);
        day.Slots[47].Should().Be(46m);
        day.TotalKwh.Should().Be(1081m);
    }

    [Fact]
    public void AddsTheRepeatedHourToItsSlotsWhenTheClocksGoBack()
    {
        var day = Place(new DateOnly(2026, 10, 25), 50);

        day.Slots.Take(5).Should().Equal(1m, 2m, 8m, 10m, 7m);
        day.Slots[47].Should().Be(50m);
        day.TotalKwh.Should().Be(1275m);
    }

    [Fact]
    public void FillsSlotsInOrderAndDropsWhatIsPastFortyEightOnAnyOtherDay()
    {
        var shortDay = Place(new DateOnly(2026, 6, 24), 46);
        var longDay = Place(new DateOnly(2026, 6, 24), 50);

        shortDay.Slots.Skip(44).Should().Equal(45m, 46m, 0m, 0m);
        longDay.Slots[47].Should().Be(48m);
        longDay.TotalKwh.Should().Be(1176m);
    }
}
