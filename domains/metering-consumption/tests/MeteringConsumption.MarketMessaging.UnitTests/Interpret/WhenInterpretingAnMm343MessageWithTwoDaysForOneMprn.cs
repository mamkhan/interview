using System.Text;
using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// Two MPRN blocks for one meter point, a day apart: a reading for each day, each under its own message id so the bus
// keeps both.
public class WhenInterpretingAnMm343MessageWithTwoDaysForOneMprn
{
    private const string Mprn = "10900000343";
    private const string Hash = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private const string ReceiptId = "6a5b4c3d-2e1f-4a0b-9c8d-7e6f5a4b3c2d";

    private const string Xml =
        """<?xml version="1.0" encoding="UTF-8"?><ieXMLDocument>"""
        + """<MessageHeader MessageTypeCode="343" VersionNumber="14.00.00" MarketTimestamp="2026-06-26T02:50:10.918Z" SenderID="DSO" RecipientID="S22" TxRefNbr="0000000705508327"/>"""
        + "<MIM343_SMARTMeteringIntervalData>"
        + """<MPRNLevelInfo MPRN="10900000343" ReadDate="2026-06-24" ReadingReplacementVersionNumber="1"><MeterID MeterCategoryCode="RM106" SerialNumber="32300001">"""
        + """<ChannelInfo MeteringInterval="30" RegisterTypeCode="70" UOM_Code="KWT"><IntervalInfo IntervalValue="0.14" IntervalPeriodTimestamp="2026-06-24T00:00:00+01:00" IntervalStatusCode="VVAK"/></ChannelInfo>"""
        + "</MeterID></MPRNLevelInfo>"
        + """<MPRNLevelInfo MPRN="10900000343" ReadDate="2026-06-25" ReadingReplacementVersionNumber="1"><MeterID MeterCategoryCode="RM106" SerialNumber="32300001">"""
        + """<ChannelInfo MeteringInterval="30" RegisterTypeCode="70" UOM_Code="KWT"><IntervalInfo IntervalValue="0.21" IntervalPeriodTimestamp="2026-06-25T00:00:00+01:00" IntervalStatusCode="VVAK"/></ChannelInfo>"""
        + "</MeterID></MPRNLevelInfo>"
        + "</MIM343_SMARTMeteringIntervalData></ieXMLDocument>";

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingAnMm343MessageWithTwoDaysForOneMprn()
    {
        var interpreter = new MM343Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 10, 0, 3, TimeSpan.Zero)),
                                            NullLogger<MM343Strategy>.Instance);
        using var payload = new MemoryStream(Encoding.UTF8.GetBytes(Xml));
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void PublishesAReadingForEachDay() =>
        _publisher.EventTypes.Should().Equal(typeof(IntervalReadingReceived), typeof(IntervalReadingReceived));

    [Fact]
    public void KeepsEachDaysReadingApart() =>
        _publisher.Events.OfType<IntervalReadingReceived>().Select(r => r.ReadDate).Should().Equal(new DateOnly(2026, 6, 24), new DateOnly(2026, 6, 25));

    [Fact]
    public void GivesEveryMessageADistinctMessageId() =>
        _publisher.Metadata.Select(m => m.MessageId).Should().OnlyHaveUniqueItems();

    [Fact]
    public void ReturnsTheTwoAsPublished() => _outcome.ReadingsPublished.Should().Be(2);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = "DSO_MIM_343_1782352213415_3805740.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];

        public List<EventMetadata> Metadata { get; } = [];

        public List<Type> EventTypes { get; } = [];

        public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
            where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

        public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
        {
            Events.Add((IEvent)message);
            Metadata.Add(metadata);
            EventTypes.Add(eventType);
            return Task.CompletedTask;
        }

        public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
