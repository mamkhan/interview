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

// The MM307 interpreter over a real ESB energisation of a 24-hour meter, its identifiers replaced by fake ones and
// every other value as ESB sent it. ESB states the estimate and the actual on separate entries with their own dates:
// an actual of 0 for the de-energised period and a fresh estimate from the day after the status change. Expected
// values read offline from the file's text, sharing no code with the parser: no configuration code, status E, load
// profile 01, DUOS group DG1, effective 2026-09-30, 24H estimated 7252.7558423 from 2026-10-01 and actual 0 from
// 2026-09-29, header MarketTimestamp 2026-09-30T18:16:16.306Z; sha256 of the file
// 74674599c1de9c5a4f87ed0d76fc9710059875197c42d14c62a6f5f68256904e (sha256sum).
public class WhenInterpretingARealMm307File
{
    private const string FileName = "ESB_MM307_10900000307_0000000790000307.xml";
    private const string Mprn = "10900000307";
    private const string Hash = "74674599c1de9c5a4f87ed0d76fc9710059875197c42d14c62a6f5f68256904e";
    private const string ReceiptId = "3e0b6a4c-8f5d-4c17-b9a2-7b4c1e3f5a06";

    private static readonly DateOnly EffectiveFrom = new(2026, 9, 30);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm307File()
    {
        var interpreter = new MM307Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 18, 30, 3, TimeSpan.Zero)),
                                            NullLogger<MM307Strategy>.Instance);
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
    public void CarriesTheEnergisedMeterPoint()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000307");
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
            new UsageFactor("24H", new DateOnly(2026, 10, 1), ActualUsageFactor: null, EstimatedUsageFactor: 7252.7558423m),
            new UsageFactor("24H", new DateOnly(2026, 9, 29), ActualUsageFactor: 0m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheStatusChangesEffectiveDate() => UsageFactors.ReadDate.Should().Be(EffectiveFrom);

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 30, 18, 30, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 9, 30, 18, 30, 0, TimeSpan.Zero),
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
