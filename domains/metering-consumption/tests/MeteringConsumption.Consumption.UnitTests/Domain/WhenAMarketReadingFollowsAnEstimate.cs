using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// RRVN 0 is reserved for an internal estimate, so any market reading (RRVN 1 or higher) replaces it: the estimate
// of 0.30 kWh a half-hour (status P) is cancelled and the actual 0.25 posted.
public class WhenAMarketReadingFollowsAnEstimate
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 22, 3, 0, 0, TimeSpan.Zero);

    private readonly LedgerEntry _actual;

    public WhenAMarketReadingFollowsAnEstimate()
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);
        ledger.Record(new MeterDayReadingBuilder().WithRrvn(0).WithUniformKwh(0.30m).WithStatus("P").Build(), PostedAt.AddDays(-1));
        _actual = ((RecordOutcome.Recorded)ledger.Record(new MeterDayReadingBuilder().WithRrvn(1).Build(), PostedAt)).Entry;
    }

    [Fact]
    public void ReplacesTheEstimate()
    {
        _actual.EntryType.Should().Be(EntryType.ReplacementReadingReceived);
        _actual.SupersedesRrvn.Should().Be(0);
    }

    [Fact]
    public void CancelsTheEstimateWithItsOwnStatus()
    {
        _actual.Reversal!.Periods.Should().OnlyContain(period => period.Kwh == -0.30m && period.Status == "P");
    }
}
