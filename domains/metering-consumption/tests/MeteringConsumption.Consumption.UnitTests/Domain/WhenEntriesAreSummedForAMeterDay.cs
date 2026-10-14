using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The conservation rule: whatever sequence of versions arrives, every row the ledger posts for a half-hour
// (both legs, every entry) sums to the latest version in effect, and a half-hour it dropped sums to 0. Each
// sequence uses three half-hours; the expected sums are the latest version's values, read off the inputs.
public class WhenEntriesAreSummedForAMeterDay
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 0, 0, TimeSpan.Zero);

    private static MeterDayReading Version(int rrvn, params decimal[] kwh) =>
        new MeterDayReadingBuilder().WithRrvn(rrvn).WithPeriodKwh(kwh).Build();

    [Fact]
    public void SumsToAFirstReading() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m)).Should().Equal(0.25m, 0.30m, 0.10m);

    [Fact]
    public void SumsToARevision() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m), Version(2, 0.20m, 0.35m, 0.10m))
            .Should().Equal(0.20m, 0.35m, 0.10m);

    [Fact]
    public void SumsToASecondRevision() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m), Version(2, 0.20m, 0.35m, 0.10m), Version(3, 0.21m, 0.36m, 0.11m))
            .Should().Equal(0.21m, 0.36m, 0.11m);

    [Fact]
    public void SumsToZeroOnAHalfHourARevisionDropped() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m), Version(2, 0.20m, 0.35m))
            .Should().Equal(0.20m, 0.35m, 0.00m);

    [Fact]
    public void SumsToTheRevisionWhenALowerVersionArrivesLate() =>
        SumsAfter(Version(2, 0.20m, 0.35m, 0.10m), Version(1, 0.25m, 0.30m, 0.10m))
            .Should().Equal(0.20m, 0.35m, 0.10m);

    [Fact]
    public void SumsToAReadingThatArrivedTwice() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m), Version(1, 0.25m, 0.30m, 0.10m))
            .Should().Equal(0.25m, 0.30m, 0.10m);

    [Fact]
    public void SumsToTheFirstOfAConflictingResend() =>
        SumsAfter(Version(1, 0.25m, 0.30m, 0.10m), Version(1, 0.99m, 0.99m, 0.99m))
            .Should().Equal(0.25m, 0.30m, 0.10m);

    [Fact]
    public void SumsToTheActualThatReplacedAnEstimate() =>
        SumsAfter(Version(0, 0.30m, 0.30m, 0.30m), Version(1, 0.25m, 0.30m, 0.10m))
            .Should().Equal(0.25m, 0.30m, 0.10m);

    [Fact]
    public void CancelsThePreviousRevisionNotTheOriginal()
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
        ledger.Record(Version(1, 0.25m, 0.30m, 0.10m), PostedAt);
        ledger.Record(Version(2, 0.20m, 0.35m, 0.10m), PostedAt);

        var third = ((RecordOutcome.Recorded)ledger.Record(Version(3, 0.21m, 0.36m, 0.11m), PostedAt)).Entry;

        third.Reversal!.Rrvn.Should().Be(2);
        third.Reversal.Periods.Select(period => period.Kwh).Should().Equal(-0.20m, -0.35m, -0.10m);
    }

    private static decimal[] SumsAfter(params MeterDayReading[] versions)
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
        foreach (var version in versions)
        {
            ledger.Record(version, PostedAt);
        }

        var rows = ledger.Entries.SelectMany(entry => (entry.Reversal?.Periods ?? []).Concat(entry.Reading.Periods)).ToArray();
        return [.. Enumerable.Range(1, 3).Select(period => rows.Where(row => row.Period == period).Sum(row => row.Kwh))];
    }
}
