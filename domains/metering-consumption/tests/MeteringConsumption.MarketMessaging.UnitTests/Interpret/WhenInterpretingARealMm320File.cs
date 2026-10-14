using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM320 interpreter over a real ESB change-of-supplier file for a day/night/peak smart meter, its identifiers
// replaced by fake ones and every other value as ESB sent it. Expected values read offline with Python's xml.etree,
// sharing no code with the parser: MCC16, load profile 25, DUOS group DG1, status E, read date 2026-09-30, estimates
// 01D 4389.66894, 01N 6402.89496, 01P 886.4361, header MarketTimestamp 2026-10-01T06:55:18.074Z; sha256 of the file
// 6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc (shasum -a 256).
public class WhenInterpretingARealMm320File
{
    private const string FileName = "ESB_MM320_10900000320_0000000790000320.xml";
    private const string Mprn = "10900000320";
    private const string Hash = "6244b8d5c3a1de7d6387cf90f2d3085471f32a1ec72fc230fe2feb592da29bfc";
    private const string ReceiptId = "5c1e8a72-94d3-4b06-a8f1-3e7d2c9b0a64";

    private static readonly DateOnly ReadDate = new(2026, 9, 30);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm320File()
    {
        var interpreter = new MM320Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 3, TimeSpan.Zero)),
                                            NullLogger<MM320Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    private UsageFactorsReceived UsageFactors => _publisher.Events.OfType<UsageFactorsReceived>().Single();

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC16");
        MeterConfiguration.LoadProfileCode.Should().Be("25");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000320");
    }

    [Fact]
    public void DatesTheConfigurationByTheReadDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().BeNull();
        MeterConfiguration.ReadDate.Should().Be(ReadDate);
    }

    [Fact]
    public void CarriesAnEstimateForEachOfTheThreeRates() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("01D", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 4389.66894m),
            new UsageFactor("01N", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 6402.89496m),
            new UsageFactor("01P", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 886.4361m));

    [Fact]
    public void SendsSupplyPointsTheWholeConfigurationAsOneEvent() =>
        _publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(
            new MeterConfigurationChangeReceivedBuilder().AsTheRealMm320States().Build(), options => options
                .ComparingRecordsByMembers()
                .Excluding(change => change.EventId)
                .Excluding(change => change.OccurredAtUtc)
                .Excluding(change => change.CorrelationId));

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
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
