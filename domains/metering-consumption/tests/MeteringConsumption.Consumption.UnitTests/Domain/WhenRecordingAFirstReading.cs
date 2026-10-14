using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// No version of the meter-day is kept yet, so the reading is its first: posted in full as a reading leg with
// nothing to cancel. The literals are the proof of concept's persona, 48 half-hours of 0.25 kWh.
public class WhenRecordingAFirstReading
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 14, 0, TimeSpan.Zero);

    private readonly MeterDayReading _reading = new MeterDayReadingBuilder().Build();
    private readonly RecordOutcome _outcome;

    public WhenRecordingAFirstReading()
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
        _outcome = ledger.Record(_reading, PostedAt);
    }

    private LedgerEntry Entry => _outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry;

    [Fact]
    public void RecordsItAsAFirstReadingThatSupersedesNothing()
    {
        Entry.EntryType.Should().Be(EntryType.ReadingReceived);
        Entry.Rrvn.Should().Be(1);
        Entry.SupersedesRrvn.Should().BeNull();
    }

    [Fact]
    public void PostsNoReversal()
    {
        Entry.Reversal.Should().BeNull();
    }

    [Fact]
    public void PostsEveryHalfHourInFullOnTheReadingLeg()
    {
        Entry.Reading.Rrvn.Should().Be(1);
        Entry.Reading.Periods.Should().HaveCount(48);
        Entry.Reading.Periods.Should().OnlyContain(period => period.Kwh == 0.25m && period.Status == "VVAK");
        Entry.Reading.Periods.Select(period => period.Period).Should().Equal(Enumerable.Range(1, 48));
        Entry.Reading.TotalKwh.Should().Be(12.00m);
    }

    [Fact]
    public void KeepsEachHalfHoursOwnStatus()
    {
        var mixed = new MeterDayReadingBuilder()
            .WithPeriodKwh(0.25m, 0.30m, 0.10m)
            .WithPeriodStatuses("VVAK", "VEST", "VCHG")
            .Build();

        var outcome = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []).Record(mixed, PostedAt);

        outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry.Reading.Periods
            .Select(period => period.Status).Should().Equal("VVAK", "VEST", "VCHG");
    }

    [Fact]
    public void IdentifiesTheEntryByTheReadingItRecords()
    {
        Entry.LedgerEntryId.Should().Be(_reading.ReadingId);
        Entry.MeterDay.Should().Be(new MeterDay("10001234567", new DateOnly(2026, 6, 20)));
        Entry.MeterSerial.Should().Be("M123456");
        Entry.MeteringIntervalMinutes.Should().Be(30);
        Entry.UomCode.Should().Be("KWT");
    }

    [Fact]
    public void PostsItAtTheTimeItWasRecorded()
    {
        Entry.PostedAtUtc.Should().Be(PostedAt);
    }

    [Fact]
    public void KeepsThePostingTimeToTheMillisecond()
    {
        var outcome = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, [])
            .Record(_reading, new DateTimeOffset(2026, 6, 21, 2, 14, 0, 417, TimeSpan.Zero).AddTicks(6_543));

        outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry.PostedAtUtc
            .Should().Be(new DateTimeOffset(2026, 6, 21, 2, 14, 0, 417, TimeSpan.Zero));
    }
}
