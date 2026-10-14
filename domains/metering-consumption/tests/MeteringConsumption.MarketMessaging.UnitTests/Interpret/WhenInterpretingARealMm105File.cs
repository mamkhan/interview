using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM105 interpreter over a real ESB change of supplier confirmation, its MPRN, references and every piece of the
// customer's personal data (name, email, phones, address) replaced by fake ones and every other value as ESB sent it;
// the smart data services arrive as MIM105_SmartDataServices. Expected values read offline from the file's text,
// sharing no code with the parser: MCC12, no load profile, DUOS group DG2, status E, effective 2026-10-01, header
// MarketTimestamp 2026-10-02T12:14:52.753Z; sha256 of the file
// c187562fa0c7ae06609dd0aa5ea03f849596a1e73928b243b0da3119e4a1d8cb (sha256sum).
public class WhenInterpretingARealMm105File
{
    private const string FileName = "ESB_MM105_10900000105_0000000790000105.xml";
    private const string Mprn = "10900000105";
    private const string Hash = "c187562fa0c7ae06609dd0aa5ea03f849596a1e73928b243b0da3119e4a1d8cb";
    private const string ReceiptId = "8d4a0e6b-2c9f-4a51-b3e7-1f8a5c7d9e40";

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm105File()
    {
        var interpreter = new MM105Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 2, 12, 30, 3, TimeSpan.Zero)),
                                            NullLogger<MM105Strategy>.Instance);
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
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC12");
        MeterConfiguration.LoadProfileCode.Should().BeNull();
        MeterConfiguration.DuosGroup.Should().Be("DG2");
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
        OccurredAtUtc = new DateTimeOffset(2026, 10, 2, 12, 30, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 2, 12, 30, 0, TimeSpan.Zero),
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
