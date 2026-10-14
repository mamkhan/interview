using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record;
using MeteringConsumption.Contracts.Events.Consumption;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The persona's first reading, 48 half-hours of 0.25 kWh, arrives: the entry is kept, then announced as
// ConsumptionRecorded under the ledger entry id. The event id is DeterministicGuid of that id, derived offline
// in Python; the posting time is the clock's, to the millisecond.
public class WhenHandlingAFirstReading
{
    private const string LedgerEntryId =
        "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00:10001234567:2026-06-20:1";

    private static readonly DateTimeOffset Now = new(2026, 6, 21, 2, 14, 0, 417, TimeSpan.Zero);

    private readonly CallLog _log = new();
    private readonly FakeLedgerStore _store;
    private readonly RecordingPublisher _publisher;

    public WhenHandlingAFirstReading()
    {
        _store = new FakeLedgerStore(_log);
        _publisher = new RecordingPublisher(_log);
        var handler = new RecordIntervalReadingReceivedEventHandler(_store, _publisher, new FixedTimeProvider(Now),
            new RecordingLogger<RecordIntervalReadingReceivedEventHandler>());

        handler.HandleAsync(new IntervalReadingReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private ConsumptionRecorded Recorded => (ConsumptionRecorded)_publisher.Published.Single().Event;

    private EventMetadata Metadata => _publisher.Published.Single().Metadata;

    [Fact]
    public void KeepsTheEntryBeforeAnnouncingIt() => _log.Calls.Should().Equal("append", "publish");

    [Fact]
    public void KeepsTheReadingInFullWithTheFileItCameFrom()
    {
        var kept = _store.Kept.Single();
        kept.Entry.LedgerEntryId.Should().Be(LedgerEntryId);
        kept.Entry.Reading.TotalKwh.Should().Be(12.00m);
        kept.Provenance.ContentHash.Should().Be(MeterDayReadingBuilder.ContentHash);
    }

    [Fact]
    public void AnnouncesItUnderTheLedgerEntryIdOnTheSupplyPointsSession()
    {
        Metadata.MessageId.Should().Be(LedgerEntryId);
        Metadata.SessionId.Should().Be("10001234567");
        Metadata.CorrelationId.Should().Be("5c2a4f1e-7d3b-4e8a-9f61-0b2c3d4e5f60");
    }

    [Fact]
    public void DerivesTheEventIdFromTheLedgerEntryId() =>
        Recorded.EventId.Should().Be(Guid.Parse("08a53c36-4197-94a8-4753-10bb40870fc7"));

    [Fact]
    public void NamesConsumptionAsThePublisherAtThePostingTime()
    {
        Recorded.PublishingContext.Should().Be("metering-consumption.consumption");
        Recorded.OccurredAtUtc.Should().Be(Now);
        Recorded.PostedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void AnnouncesAFirstReadingWithNothingCancelled()
    {
        Recorded.EntryType.Should().Be("ReadingReceived");
        Recorded.RRVN.Should().Be(1);
        Recorded.SupersedesRRVN.Should().BeNull();
        Recorded.Reversal.Should().BeNull();
        Recorded.Reading.RRVN.Should().Be(1);
        Recorded.Reading.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == 0.25m && period.Status == "VVAK");
        Recorded.Reading.TotalKwh.Should().Be(12.00m);
    }

    [Fact]
    public void DescribesTheMeterDayItRecords()
    {
        Recorded.SupplyPointId.Should().Be("10001234567");
        Recorded.MeterSerial.Should().Be("M123456");
        Recorded.ReadDate.Should().Be(new DateOnly(2026, 6, 20));
        Recorded.MeteringIntervalMinutes.Should().Be(30);
        Recorded.UOMCode.Should().Be("KWT");
        Recorded.Fuel.Should().Be("electricity");
        Recorded.Provenance.Market.Should().Be("roi-esb");
        Recorded.Provenance.ContentHash.Should().Be(MeterDayReadingBuilder.ContentHash);
    }
}
