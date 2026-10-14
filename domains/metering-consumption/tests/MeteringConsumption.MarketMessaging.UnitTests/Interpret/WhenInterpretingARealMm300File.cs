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

// The MM300 interpreter over a real ESB scheduled reading for a 24-hour meter with a night-storage meter beside it,
// its identifiers replaced by fake ones and every other value as ESB sent it. ESB states the estimate and the actual
// on separate entries with their own dates, and a zero factor as 0. Expected values read offline from the file's
// text, sharing no code with the parser: no configuration code, load profile 01, DUOS group DG1, status E, read date
// 2026-09-30, 00N estimated 0 and 24H estimated 1751.17679457 from 2026-09-30, 00N actual 0 and 24H actual
// 355.75636533 from 2026-02-27, header MarketTimestamp 2026-10-01T13:49:46.228Z; sha256 of the file
// a53cb4e26c506eb8880273430806a526c509015861377a38d1f792f77b3b1271 (sha256sum).
public class WhenInterpretingARealMm300File
{
    private const string FileName = "ESB_MM300_10900000300_0000000790000300.xml";
    private const string Mprn = "10900000300";
    private const string Hash = "a53cb4e26c506eb8880273430806a526c509015861377a38d1f792f77b3b1271";
    private const string ReceiptId = "e7d3a915-4c2b-4f8e-b061-9a5c3d2e1f70";

    private static readonly DateOnly ReadDate = new(2026, 9, 30);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm300File()
    {
        var interpreter = new MM300Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 14, 0, 3, TimeSpan.Zero)),
                                            NullLogger<MM300Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    private UsageFactorsReceived UsageFactors => _publisher.Events.OfType<UsageFactorsReceived>().Single();

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000300");
    }

    [Fact]
    public void LeavesTheMeterConfigurationCodeItDoesNotCarryEmpty() =>
        MeterConfiguration.MeterConfigurationCode.Should().BeNull();

    [Fact]
    public void DatesTheConfigurationByTheReading()
    {
        MeterConfiguration.EffectiveFromDate.Should().BeNull();
        MeterConfiguration.ReadDate.Should().Be(ReadDate);
    }

    [Fact]
    public void CarriesTheEstimatesAndTheActualsAsSeparateFactorsWithTheirOwnDates() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("00N", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 0m),
            new UsageFactor("24H", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 1751.17679457m),
            new UsageFactor("00N", new DateOnly(2026, 2, 27), ActualUsageFactor: 0m, EstimatedUsageFactor: null),
            new UsageFactor("24H", new DateOnly(2026, 2, 27), ActualUsageFactor: 355.75636533m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheReading() => UsageFactors.ReadDate.Should().Be(ReadDate);

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero),
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
