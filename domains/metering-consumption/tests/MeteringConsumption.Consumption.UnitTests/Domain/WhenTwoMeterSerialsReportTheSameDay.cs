using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A meter exchange leaves two devices reporting one supply point. Consumption is continuous per supply point,
// as in V1, so the second device is not a second series: the version rules decide, and the outcome names the
// meter whose version was replaced so the exchange can be logged. M123456 reports 0.25 kWh a half-hour first.
public class WhenTwoMeterSerialsReportTheSameDay
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 30, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);

    public WhenTwoMeterSerialsReportTheSameDay()
    {
        _ledger.Record(new MeterDayReadingBuilder().WithUniformKwh(0.25m).Build(), PostedAt.AddHours(-1));
    }

    [Fact]
    public void TreatsTheSameVersionAndValuesFromAnotherMeterAsAlreadyRecorded()
    {
        var outcome = _ledger.Record(
            new MeterDayReadingBuilder().FromMeter(MeterDayReadingBuilder.ExchangedMeterSerial).Build(), PostedAt);

        outcome.Should().BeOfType<RecordOutcome.AlreadyRecorded>();
    }

    [Fact]
    public void CancelsTheOtherMetersVersionWhenAHigherOneArrives()
    {
        var entry = RecordFromTheExchangedMeter().Entry;

        entry.Reversal!.Periods.Should().OnlyContain(period => period.Kwh == -0.25m);
        entry.Reading.Periods.Should().OnlyContain(period => period.Kwh == 0.30m);
        entry.MeterSerial.Should().Be("M999888");
    }

    [Fact]
    public void NamesTheMeterWhoseVersionWasReplaced()
    {
        RecordFromTheExchangedMeter().ReplacedMeterSerial.Should().Be("M123456");
    }

    [Fact]
    public void NamesNoMeterWhenOneMeterReportsTheDay()
    {
        var outcome = _ledger.Record(new MeterDayReadingBuilder().WithRrvn(2).Build(), PostedAt);

        outcome.Should().BeOfType<RecordOutcome.Recorded>().Subject.ReplacedMeterSerial.Should().BeNull();
    }

    private RecordOutcome.Recorded RecordFromTheExchangedMeter() =>
        (RecordOutcome.Recorded)_ledger.Record(new MeterDayReadingBuilder()
            .FromMeter(MeterDayReadingBuilder.ExchangedMeterSerial).WithRrvn(2).WithUniformKwh(0.30m).Build(), PostedAt);
}
