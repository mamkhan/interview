using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Reads of 12 kWh a day up to 2026-04-30, so the anchor is 2026-05-01 and the baseline can fall from 2026-04-02
// (29 days back) to 2026-04-24 (7 days back). Ten days from 04-21 count 9 × 12 = 108 kWh after the baseline.
public sealed class WhenFindingTheOwnUseWindow
{
    private static readonly DateOnly LatestReadDate = new(2026, 4, 30);

    private static PufWindow? Find(ConsumptionWindowBuilder days) => days.Build().FindPufWindow(ForecastPersonas.Settings);

    [Fact]
    public void TakesTheOldestReadInRangeAsTheBaselineAndCountsTheDaysAfterIt()
    {
        var window = Find(new ConsumptionWindowBuilder().DailyUse(LatestReadDate, 10, 12m))!;

        window.Baseline.Should().Be(new DateOnly(2026, 4, 21));
        window.FirstCountedDay.Should().Be(new DateOnly(2026, 4, 22));
        window.Days.Should().Be(9);
        window.TotalKwh.Should().Be(108m);
    }

    [Fact]
    public void StartsFromAReadExactlySevenDaysBeforeTheAnchor()
    {
        var window = Find(new ConsumptionWindowBuilder().DailyUse(LatestReadDate, 7, 12m))!;

        window.Baseline.Should().Be(new DateOnly(2026, 4, 24));
        window.TotalKwh.Should().Be(72m);
    }

    [Fact]
    public void FindsNoWindowWithFewerThanSevenDays()
    {
        Find(new ConsumptionWindowBuilder().DailyUse(LatestReadDate, 6, 12m)).Should().BeNull();
    }

    [Fact]
    public void FindsNoWindowWhenTheOnlyEarlierReadIsOlderThanTwentyNineDays()
    {
        var days = new ConsumptionWindowBuilder()
            .DailyUse(new DateOnly(2026, 4, 1), 1, 12m)
            .DailyUse(LatestReadDate, 6, 12m);

        Find(days).Should().BeNull();
    }

    [Fact]
    public void CountsNothingForADayWithNoReading()
    {
        var days = new ConsumptionWindowBuilder()
            .DailyUse(new DateOnly(2026, 4, 21), 1, 12m)
            .DailyUse(LatestReadDate, 8, 12m);

        var window = Find(days)!;

        window.Days.Should().Be(9);
        window.TotalKwh.Should().Be(96m);
    }

    [Fact]
    public void RefusesAWindowWithNoReadDay()
    {
        var act = () => new ConsumptionWindowBuilder().Build();

        act.Should().Throw<ForecastInputRejectedException>();
    }
}
