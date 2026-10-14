using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// RRVN 1 is kept at 0.25 kWh a half-hour (VVAK) when RRVN 2 arrives at 0.20 (VCHG): the entry cancels RRVN 1
// in full, -0.25 with its own status, and posts RRVN 2 in full, +0.20. The net, -0.05, is the proof of
// concept's difference for the same persona.
public class WhenRecordingAReplacement
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 22, 2, 11, 12, TimeSpan.Zero);

    private readonly RecordOutcome _outcome;

    public WhenRecordingAReplacement()
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
        ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.25m).Build(), PostedAt.AddDays(-1));
        _outcome = ledger.Record(
            new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.20m).WithStatus("VCHG").Build(), PostedAt);
    }

    private LedgerEntry Entry => _outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry;

    [Fact]
    public void RecordsItAsAReplacementOfThePriorVersion()
    {
        Entry.EntryType.Should().Be(EntryType.ReplacementReadingReceived);
        Entry.Rrvn.Should().Be(2);
        Entry.SupersedesRrvn.Should().Be(1);
    }

    [Fact]
    public void CancelsThePriorVersionInFullWithItsOwnStatus()
    {
        Entry.Reversal!.Rrvn.Should().Be(1);
        Entry.Reversal.Periods.Should().HaveCount(48);
        Entry.Reversal.Periods.Should().OnlyContain(period => period.Kwh == -0.25m && period.Status == "VVAK");
        Entry.Reversal.TotalKwh.Should().Be(-12.00m);
    }

    [Fact]
    public void PostsTheNewVersionInFullWithItsOwnStatus()
    {
        Entry.Reading.Rrvn.Should().Be(2);
        Entry.Reading.Periods.Should().OnlyContain(period => period.Kwh == 0.20m && period.Status == "VCHG");
        Entry.Reading.TotalKwh.Should().Be(9.60m);
    }

    [Fact]
    public void NetsToTheDifferenceOnEveryHalfHour()
    {
        Entry.Reversal!.Periods.Zip(Entry.Reading.Periods, (reversed, read) => reversed.Kwh + read.Kwh)
            .Should().OnlyContain(net => net == -0.05m);
    }
}
