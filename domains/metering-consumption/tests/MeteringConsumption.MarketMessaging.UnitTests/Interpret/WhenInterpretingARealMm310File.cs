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

// The MM310 interpreter over a real ESB change-of-supplier closing read for a day/night/peak smart meter, its
// identifiers replaced by fake ones and every other value as ESB sent it. A 310 states actual usage factors only.
// Expected values read offline from the file's text, sharing no code with the parser: no configuration code, status
// E, load profile 25, DUOS group DG1, read date 2026-09-28, actuals 01D 1331.76627221, 01N 709.1223106 and 01P
// 259.43498585 from 2026-09-04, header MarketTimestamp 2026-10-01T19:32:06.581Z; sha256 of the file
// 9ab8c6a236acc76e2922abace4d52bba68d3be7b8f180cb85d231a7a2e5f2bb4 (sha256sum).
public class WhenInterpretingARealMm310File
{
    private const string FileName = "ESB_MM310_10900000310_0000000790000310.xml";
    private const string Mprn = "10900000310";
    private const string Hash = "9ab8c6a236acc76e2922abace4d52bba68d3be7b8f180cb85d231a7a2e5f2bb4";
    private const string ReceiptId = "4f1c7b5d-9a6e-4d28-a0b3-8c5d2f4a6b17";

    private static readonly DateOnly ReadDate = new(2026, 9, 28);
    private static readonly DateOnly FactorsEffectiveFrom = new(2026, 9, 4);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm310File()
    {
        var interpreter = new MM310Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 19, 45, 3, TimeSpan.Zero)),
                                            NullLogger<MM310Strategy>.Instance);
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
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.LoadProfileCode.Should().Be("25");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000310");
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
    public void CarriesAnActualFactorForEveryTimeslot() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("01D", FactorsEffectiveFrom, ActualUsageFactor: 1331.76627221m, EstimatedUsageFactor: null),
            new UsageFactor("01N", FactorsEffectiveFrom, ActualUsageFactor: 709.1223106m, EstimatedUsageFactor: null),
            new UsageFactor("01P", FactorsEffectiveFrom, ActualUsageFactor: 259.43498585m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheReading() => UsageFactors.ReadDate.Should().Be(ReadDate);

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 10, 1, 19, 45, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 10, 1, 19, 45, 0, TimeSpan.Zero),
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
