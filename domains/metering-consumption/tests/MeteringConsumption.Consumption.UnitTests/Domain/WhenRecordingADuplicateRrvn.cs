using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// RRVN 1 is kept when RRVN 1 arrives again with the same values, from another file or as a redelivery: nothing
// new is posted, and the entry already kept comes back so it can be announced again under its own id.
public class WhenRecordingADuplicateRrvn
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 14, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
    private readonly LedgerEntry _kept;

    public WhenRecordingADuplicateRrvn()
    {
        _kept = ((RecordOutcome.Recorded)_ledger.Record(new MeterDayReadingBuilder().Build(), PostedAt)).Entry;
    }

    [Fact]
    public void ReturnsTheEntryAlreadyKept()
    {
        var outcome = _ledger.Record(new MeterDayReadingBuilder().Build(), PostedAt.AddHours(1));

        outcome.Should().BeOfType<RecordOutcome.AlreadyRecorded>().Subject.Stored.Should().BeSameAs(_kept);
    }

    [Fact]
    public void ReturnsTheEntryAlreadyKeptWhenAnotherFileRepeatsTheVersion()
    {
        var outcome = _ledger.Record(new MeterDayReadingBuilder().FromFile("another-file").Build(), PostedAt.AddDays(1));

        outcome.Should().BeOfType<RecordOutcome.AlreadyRecorded>().Subject.Stored.LedgerEntryId
            .Should().Be(_kept.LedgerEntryId);
    }

    [Fact]
    public void PostsNothingNew()
    {
        _ledger.Record(new MeterDayReadingBuilder().Build(), PostedAt.AddHours(1));

        _ledger.Entries.Should().ContainSingle();
    }
}
