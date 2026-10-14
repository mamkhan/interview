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

// The MM306 interpreter over a real ESB de-energisation of a 24-hour meter, its identifiers replaced by fake ones and
// every other value as ESB sent it. ESB states the estimate and the actual on separate entries with their own dates,
// the estimate zeroed from the day after the status change. Expected values read offline from the file's text,
// sharing no code with the parser: no configuration code, status DR, load profile 03, DUOS group DG2, effective
// 2026-09-30, 24H estimated 0 from 2026-10-01 and actual 2803.66425481 from 2026-08-15, header MarketTimestamp
// 2026-10-01T15:18:28.366Z; sha256 of the file e171c40e8dbf5cd57eb17e0dd2f0972b8b80a5e136aa9bb37515e81af5c976cb
// (sha256sum).
public class WhenInterpretingARealMm306File
{
    private const string FileName = "ESB_MM306_10900000306_0000000790000306.xml";
    private const string Mprn = "10900000306";
    private const string Hash = "e171c40e8dbf5cd57eb17e0dd2f0972b8b80a5e136aa9bb37515e81af5c976cb";
    private const string ReceiptId = "2d9a5f3b-7e4c-4b06-a8f1-6a3b0d2e4f95";

    private static readonly DateOnly EffectiveFrom = new(2026, 9, 30);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm306File()
    {
        var interpreter = new MM306Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 15, 30, 3, TimeSpan.Zero)),
                                            NullLogger<MM306Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    private UsageFactorsReceived UsageFactors => _publisher.Events.OfType<UsageFactorsReceived>().Single();

    [Fact]
    public void PublishesTheMeterConfigurationThenTheUsageFactorsThenTheChange() =>
        _publisher.Events.Select(e => e.GetType()).Should().Equal(typeof(MeterConfigurationReceived), typeof(UsageFactorsReceived),
            typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheDeEnergisedMeterPoint()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterPointStatusCode.Should().Be("DR");
        MeterConfiguration.LoadProfileCode.Should().Be("03");
        MeterConfiguration.DuosGroup.Should().Be("DG2");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000306");
    }

    [Fact]
    public void LeavesTheMeterConfigurationCodeItDoesNotCarryEmpty() =>
        MeterConfiguration.MeterConfigurationCode.Should().BeNull();

    [Fact]
    public void DatesTheConfigurationByTheStatusChangesEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(EffectiveFrom);
        MeterConfiguration.ReadDate.Should().BeNull();
    }

    [Fact]
    public void CarriesTheEstimateAndTheActualAsSeparateFactorsWithTheirOwnDates() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("24H", new DateOnly(2026, 10, 1), ActualUsageFactor: null, EstimatedUsageFactor: 0m),
            new UsageFactor("24H", new DateOnly(2026, 8, 15), ActualUsageFactor: 2803.66425481m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheStatusChangesEffectiveDate() => UsageFactors.ReadDate.Should().Be(EffectiveFrom);

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 15, 30, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 15, 30, 0, TimeSpan.Zero),
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
