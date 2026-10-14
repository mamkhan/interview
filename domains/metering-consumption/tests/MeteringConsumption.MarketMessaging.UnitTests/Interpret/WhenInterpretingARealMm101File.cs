using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM101 interpreter over a real ESB new connection registration, its MPRN, references and every piece of the
// customer's personal data (name, phones, address) replaced by fake ones and every other value as ESB sent it; the
// technical contact arrives as an empty element. Expected values read offline from the file's text, sharing no code
// with the parser: MCC02, load profile 02, DUOS group DG1, status E, effective 2026-10-01, header MarketTimestamp
// 2026-10-01T13:35:49.375Z; sha256 of the file d51ad11efda850d33b1562a1515fd9aa8590bc5731c9ab4325fd8a242f67765e
// (sha256sum).
public class WhenInterpretingARealMm101File
{
    private const string FileName = "ESB_MM101_10900000101_0000000790000101.xml";
    private const string Mprn = "10900000101";
    private const string Hash = "d51ad11efda850d33b1562a1515fd9aa8590bc5731c9ab4325fd8a242f67765e";
    private const string ReceiptId = "7c3f9d5a-1b8e-4f40-a2d6-0e7f4b6c8d39";

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm101File()
    {
        var interpreter = new MM101Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 13, 45, 3, TimeSpan.Zero)),
                                            NullLogger<MM101Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _publisher.Events.Select(e => e.GetType()).Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC02");
        MeterConfiguration.LoadProfileCode.Should().Be("02");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().BeNull();
    }

    [Fact]
    public void DatesTheConfigurationByItsEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(new DateOnly(2026, 10, 1));
        MeterConfiguration.ReadDate.Should().BeNull();
    }

    [Fact]
    public void ReturnsBothAsPublished() => _outcome.ReadingsPublished.Should().Be(2);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 13, 45, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 13, 45, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<IEvent> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
            where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

        public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
        {
            Events.Add((IEvent)message);
            return Task.CompletedTask;
        }

        public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
