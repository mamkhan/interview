using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A clock-change day carries 46 (spring) or 50 (autumn) half-hours, not 48. The legs align by period ordinal,
// so an entry has as many half-hours as its reading, each with a status.
public class WhenRecordingADstDay
{
    private static readonly DateTimeOffset PostedAt = new(2026, 10, 26, 2, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(46)]
    [InlineData(50)]
    public void PostsOneHalfHourPerPeriodAlignedByOrdinal(int periods)
    {
        var outcome = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, [])
            .Record(new MeterDayReadingBuilder().WithUniformKwh(0.25m, periods).Build(), PostedAt);

        var reading = outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry.Reading;
        reading.Periods.Select(period => period.Period).Should().Equal(Enumerable.Range(1, periods));
        reading.Periods.Should().OnlyContain(period => period.Status == "VVAK");
    }
}
