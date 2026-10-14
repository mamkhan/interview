using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A reading our own publisher should never send: no retry can fix it, so it is rejected before anything is
// posted and the message is dead-lettered. The ledger keeps kWh to six decimals, so more would be rounded away.
public class WhenAReadingBreaksItsContract
{
    private static readonly DateTimeOffset PostedAt = new(2026, 6, 21, 2, 14, 0, TimeSpan.Zero);

    private readonly MeterDayLedger _ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, []);

    [Fact]
    public void RejectsAReadingWithNoHalfHours()
    {
        Recording(new MeterDayReadingBuilder().WithNoPeriods().Build()).Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void RejectsANegativeVersion()
    {
        Recording(new MeterDayReadingBuilder().WithRrvn(-1).Build()).Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void RejectsABlankMeterSerial()
    {
        Recording(new MeterDayReadingBuilder().FromMeter(" ").Build()).Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void RejectsAHalfHourStatedTwice()
    {
        var reading = new MeterDayReadingBuilder().Build();

        Recording(reading with { Periods = [.. reading.Periods, reading.Periods[0]] })
            .Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void RejectsKwhWithMoreThanSixDecimals()
    {
        Recording(new MeterDayReadingBuilder().WithPeriodKwh(0.1234567m).Build()).Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void AcceptsKwhWithSixDecimals()
    {
        Recording(new MeterDayReadingBuilder().WithPeriodKwh(0.123456m).Build()).Should().NotThrow();
    }

    [Fact]
    public void RejectsABlankSupplyPoint()
    {
        var naming = () => new MeterDay(" ", new DateOnly(2026, 6, 20));

        naming.Should().Throw<LedgerEntryRejectedException>();
    }

    [Fact]
    public void PostsNothingForARejectedReading()
    {
        Recording(new MeterDayReadingBuilder().WithNoPeriods().Build()).Should().Throw<LedgerEntryRejectedException>();

        _ledger.Entries.Should().BeEmpty();
    }

    private Action Recording(MeterDayReading reading) => () => _ledger.Record(reading, PostedAt);
}
