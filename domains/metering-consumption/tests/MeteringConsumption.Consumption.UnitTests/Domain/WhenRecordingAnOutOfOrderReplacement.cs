using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// RRVN 2 (0.20 kWh a half-hour) is kept when RRVN 1 (0.25) arrives late, a redelivery or an estimate raced by the
// real reading. It never becomes the version in effect, so it posts nothing: posting it would count its 0.25
// on top of the 0.20 already in effect.
public class WhenRecordingAnOutOfOrderReplacement
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 23, 2, 0, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
    private readonly RecordOutcome _outcome;

    public WhenRecordingAnOutOfOrderReplacement()
    {
        _ledger.Record(new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.20m).Build(), PostedAt.AddDays(-1));
        _outcome = _ledger.Record(new MeterDayReadingBuilder().WithRrvn(1).WithUniformKwh(0.25m).Build(), PostedAt);
    }

    [Fact]
    public void ReportsTheVersionThatAlreadySupersededIt()
    {
        _outcome.Should().BeOfType<RecordOutcome.AlreadySuperseded>().Subject.EffectiveRrvn.Should().Be(2);
    }

    [Fact]
    public void PostsNothing()
    {
        _ledger.Entries.Should().ContainSingle().Which.Rrvn.Should().Be(2);
    }
}
