using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using Microsoft.Extensions.Logging.Abstractions;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM343 interpreter over the real extract (two MPRN blocks): one reading per meter-day, keyed and
// identified so the ledger's ordering and idempotency hold. Frozen literals as in WhenParsingARealEsbIntervalFile:
// MPRN 10900000343 period 1 = 0.14 KWT, MPRN 10900001343 period 1 = 0.069 KWT, both at 2026-06-24T00:00+01:00; sha256 of the extract
// e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304 (shasum -a 256).
public class WhenInterpretingAnMm343Message
{
    private const string SamplePath = "samples/DSO_MIM_343_1782352213415_3805737.extract.xml";
    private const string Hash = "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304";
    private const string ReceiptId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";

    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingAnMm343Message()
    {
        var interpreter = new MM343Strategy(_publisher, new FixedTimeProvider(Now), NullLogger<MM343Strategy>.Instance);
        using var payload = File.OpenRead(SamplePath);
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void PublishesOneReadingPerMeterDay()
    {
        _publisher.Readings.Select(r => r.MPRN).Should().Equal("10900000343", "10900001343");
        _publisher.Readings[0].Periods[0].Value.Should().Be(0.14m);
        _publisher.Readings[1].Periods[0].Value.Should().Be(0.069m);
        _publisher.Readings[0].Periods[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.FromHours(1)));
        _publisher.Readings.Should().AllSatisfy(r => r.OccurredAtUtc.Should().Be(Now));
    }

    [Fact]
    public void CarriesTheOperatorsAveragePowerAsStated()
    {
        // The channel is KWT, average power over the interval, and the values pass through unconverted,
        // so the day sums are the file's own.
        _publisher.Readings.Should().AllSatisfy(r => r.UOMCode.Should().Be("KWT"));
        _publisher.Readings[0].Periods.Sum(p => p.Value).Should().Be(17.441m);
        _publisher.Readings[1].Periods.Sum(p => p.Value).Should().Be(6.373m);
    }

    [Fact]
    public void KeysEachReadingByItsMprnSession() =>
        _publisher.Metadata.Select(m => m.SessionId).Should().Equal("10900000343", "10900001343");

    [Fact]
    public void DerivesTheReadingIdFromTheSealedFile()
    {
        _publisher.Metadata[0].MessageId.Should().Be($"{Hash}:10900000343:2026-06-24:1");
        _publisher.Readings[0].ReadingId.Should().Be($"{Hash}:10900000343:2026-06-24:1");
    }

    [Fact]
    public void CarriesProvenanceToTheSealedFile()
    {
        var provenance = _publisher.Readings[0].Provenance;

        provenance.BlobPath.Should().Be($"roi-esb/{Hash}.xml");
        provenance.ContentHash.Should().Be(Hash);
        provenance.Market.Should().Be("roi-esb");
        provenance.SourceMessageType.Should().Be("343");
        provenance.ReceivedFileName.Should().Be("DSO_MIM_343_1782352213415_3805737.xml");
        provenance.MarketTimestamp.Should().Be(new DateTimeOffset(2026, 6, 25, 2, 50, 10, 918, TimeSpan.Zero));
        _publisher.Readings[0].PublishingContext.Should().Be("metering-consumption.market-messaging");
    }

    [Fact]
    public void ReturnsTheNumberPublished() => _outcome.ReadingsPublished.Should().Be(2);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];

        public List<EventMetadata> Metadata { get; } = [];

        public List<Type> EventTypes { get; } = [];

        public List<IntervalReadingReceived> Readings => [.. Events.OfType<IntervalReadingReceived>()];

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
