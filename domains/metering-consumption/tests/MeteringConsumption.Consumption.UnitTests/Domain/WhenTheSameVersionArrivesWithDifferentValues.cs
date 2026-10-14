using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// RRVN 1 is kept at 0.25 kWh a half-hour when RRVN 1 arrives again at 0.30, or with one half-hour's status
// changed: a market version cannot mean two things, so the first one received stays and the second is flagged.
public class WhenTheSameVersionArrivesWithDifferentValues
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 14, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
    private readonly LedgerEntry _kept;

    public WhenTheSameVersionArrivesWithDifferentValues()
    {
        _kept = ((RecordOutcome.Recorded)_ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.25m).Build(), PostedAt)).Entry;
    }

    [Fact]
    public void FlagsItAsAConflictingResendOfTheEntryKept()
    {
        var outcome = _ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.30m).Build(), PostedAt.AddHours(1));

        outcome.Should().BeOfType<RecordOutcome.ConflictingResend>().Subject.Stored.Should().BeSameAs(_kept);
    }

    [Fact]
    public void FlagsAChangedStatusAsAConflictingResend()
    {
        var outcome = _ledger.Record(new MeterDayReadingBuilder().WithStatus("VEST").Build(), PostedAt.AddHours(1));

        outcome.Should().BeOfType<RecordOutcome.ConflictingResend>();
    }

    [Fact]
    public void KeepsTheFirstVersionAndPostsNothing()
    {
        _ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.30m).Build(), PostedAt.AddHours(1));

        _ledger.Entries.Should().ContainSingle().Which.Reading.TotalKwh.Should().Be(12.00m);
    }
}
