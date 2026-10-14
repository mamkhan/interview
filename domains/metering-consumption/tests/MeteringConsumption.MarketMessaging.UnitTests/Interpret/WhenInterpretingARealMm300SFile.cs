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

// The MM300S interpreter over a real ESB special reading for a 24-hour meter, its identifiers replaced by fake ones
// and every other value as ESB sent it. ESB states the estimate and the actual on separate entries with their own
// dates, the estimate effective the day after the reading. Expected values read offline from the file's text, sharing
// no code with the parser: no configuration code, load profile 01, DUOS group DG1, status E, read date 2026-09-28,
// 24H estimated 1847.49762189 from 2026-09-29 and actual 464.15887798 from 2026-09-12, header MarketTimestamp
// 2026-09-29T13:37:47.653Z; sha256 of the file 7f966eec5803b2d75984497cfbf2aaf8ea1d92666ab98b50061f19db70378956
// (sha256sum).
public class WhenInterpretingARealMm300SFile
{
    private const string FileName = "ESB_MM300S_10900000301_0000000790000301.xml";
    private const string Mprn = "10900000301";
    private const string Hash = "7f966eec5803b2d75984497cfbf2aaf8ea1d92666ab98b50061f19db70378956";
    private const string ReceiptId = "1c8f4e2a-6d3b-4a95-b7e0-5f2a9c1d3e84";

    private static readonly DateOnly ReadDate = new(2026, 9, 28);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm300SFile()
    {
        var interpreter = new MM300SStrategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 14, 0, 3, TimeSpan.Zero)),
                                             NullLogger<MM300SStrategy>.Instance);
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
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000301");
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
    public void CarriesTheEstimateAndTheActualAsSeparateFactorsWithTheirOwnDates() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("24H", new DateOnly(2026, 9, 29), ActualUsageFactor: null, EstimatedUsageFactor: 1847.49762189m),
            new UsageFactor("24H", new DateOnly(2026, 9, 12), ActualUsageFactor: 464.15887798m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheReading() => UsageFactors.ReadDate.Should().Be(ReadDate);

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 29, 14, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 9, 29, 14, 0, 0, TimeSpan.Zero),
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
