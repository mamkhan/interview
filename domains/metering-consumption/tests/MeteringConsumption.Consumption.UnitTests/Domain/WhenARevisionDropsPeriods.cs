using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// An autumn clock-change day first sent with 50 half-hours of 0.10 kWh, revised to 48 of 0.10. Cancelling
// the whole prior version cancels half-hours 49 and 50 too: reversal 50 × -0.10 = -5.00, reading
// 48 × 0.10 = +4.80, net -0.20, and each dropped half-hour sums to 0 across the ledger. The proof of concept
// kept them counted, so an hour of consumption was counted twice.
public class WhenARevisionDropsPeriods
{
    private static readonly DateTimeOffset PostedAt = new(2026, 10, 27, 2, 0, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
    private readonly LedgerEntry _revision;

    public WhenARevisionDropsPeriods()
    {
        _ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.10m, 50).Build(), PostedAt.AddDays(-1));
        _revision = ((RecordOutcome.Recorded)_ledger.Record(
            new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.10m, 48).Build(), PostedAt)).Entry;
    }

    [Fact]
    public void CancelsEveryHalfHourOfThePriorVersion()
    {
        _revision.Reversal!.Periods.Should().HaveCount(50);
        _revision.Reversal.TotalKwh.Should().Be(-5.00m);
    }

    [Fact]
    public void PostsOnlyTheHalfHoursOfTheRevision()
    {
        _revision.Reading.Periods.Should().HaveCount(48);
        _revision.Reading.TotalKwh.Should().Be(4.80m);
    }

    [Fact]
    public void LeavesTheDroppedHalfHoursAtZero()
    {
        var rows = _ledger.Entries.SelectMany(entry => (entry.Reversal?.Periods ?? []).Concat(entry.Reading.Periods));

        rows.Where(row => row.Period is 49 or 50).GroupBy(row => row.Period).Select(group => group.Sum(row => row.Kwh))
            .Should().Equal(0.00m, 0.00m);
    }
}
