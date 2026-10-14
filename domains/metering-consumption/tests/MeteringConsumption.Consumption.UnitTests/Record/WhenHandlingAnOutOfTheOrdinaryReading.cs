using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The readings that post nothing or are refused: a late older version, a reading from a market this domain does
// not know, and one with no half-hours. And the one that posts but is worth a warning: a meter exchange.
public class WhenHandlingAnOutOfTheOrdinaryReading
{
    private readonly RecordHandling _handling = new();

    [Fact]
    public async Task PostsAndAnnouncesNothingForAVersionOlderThanTheOneInEffect()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().WithRrvn(2).Build());

        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().WithRrvn(1).Build());

        _handling.Store.Appends.Should().Be(1);
        _handling.Publisher.Published.Should().ContainSingle();
        _handling.Logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RejectsAReadingFromAMarketWithNoKnownFuel()
    {
        var delivering = () => _handling.DeliverAsync(new IntervalReadingReceivedBuilder().FromMarket("atlantis").Build());

        await delivering.Should().ThrowAsync<LedgerEntryRejectedException>();
        _handling.Log.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectsAReadingWithNoHalfHours()
    {
        var delivering = () => _handling.DeliverAsync(new IntervalReadingReceivedBuilder().WithPeriods().Build());

        await delivering.Should().ThrowAsync<LedgerEntryRejectedException>();
        _handling.Log.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordsAMeterExchangeAndWarnsNamingBothMeters()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());

        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().FromMeter(MeterDayReadingBuilder.ExchangedMeterSerial)
            .WithRrvn(2).WithUniformValue(0.60m).Build());

        _handling.Announced.Last().MeterSerial.Should().Be("M999888");
        _handling.Logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains("M999888") && entry.Message.Contains("M123456"));
    }

    [Fact]
    public async Task CarriesTheGasFuelForAGasMarket()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().FromMarket("roi-gni")
            .WithPeriods(IntervalReadingReceivedBuilder.HalfHour(1, 25m, "A")).Build());

        _handling.Announced.Single().Fuel.Should().Be("gas");
    }
}
