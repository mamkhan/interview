using MeteringConsumption.Contracts.Events;
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

// The MM332 interpreter over the builder's technical details: the meter configuration first, then its usage factors,
// then the whole configuration, the retained meter and its register included, as one change for SupplyPoints, all
// keyed by MPRN. Frozen literals from
// MarketMessageXmlBuilder: MPRN 10012345678, MCC01, load profile 01, DUOS group DG1, status E, effective 2026-06-24, DAY
// estimated 2.4100 and actual 2.5000 on separate entries, NIGHT estimated 1.1300, meter 32300001 (RM106) kept with
// register 1 (00D, read 12345 since 2026-03-24), header MarketTimestamp 2026-06-25T02:50:10.918Z; the hash is the
// receipt's claim.
public class WhenInterpretingAnMm332Message
{
    private static readonly DateOnly EffectiveFrom = new(2026, 6, 24);

    private readonly Mm332Scenario _scenario = new(new MarketMessageXmlBuilder());

    [Fact]
    public void PublishesTheConfigurationThenTheUsageFactorsThenTheChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(UsageFactorsReceived),
            typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        var configuration = _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

        configuration.MPRN.Should().Be(Mm332Scenario.Mprn);
        configuration.MeterConfigurationCode.Should().Be("MCC01");
        configuration.LoadProfileCode.Should().Be("01");
        configuration.DuosGroup.Should().Be("DG1");
        configuration.MeterPointStatusCode.Should().Be("E");
        configuration.EffectiveFromDate.Should().Be(EffectiveFrom);
        configuration.ReadDate.Should().BeNull();
        configuration.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
    }

    [Fact]
    public void CarriesTheActualAndEstimatedUsageFactorsEffectiveFromTheConfigurationDate()
    {
        var usageFactors = _scenario.Publisher.Events.OfType<UsageFactorsReceived>().Single();

        usageFactors.ReadDate.Should().Be(EffectiveFrom);
        usageFactors.UsageFactors.Should().Equal(
            new UsageFactor("00D", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 2.4100m),
            new UsageFactor("00D", EffectiveFrom, ActualUsageFactor: 2.5000m, EstimatedUsageFactor: null),
            new UsageFactor("00N", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 1.1300m));
    }

    [Fact]
    public void SendsSupplyPointsTheSameConfigurationAsOneChange()
    {
        var configuration = _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

        _scenario.Publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(new
        {
            configuration.MPRN,
            configuration.NetworksReferenceNumber,
            configuration.EffectiveFromDate,
            configuration.ReadDate,
            configuration.CorrelationId,
            configuration.Provenance,
            Configuration = new
            {
                configuration.MeterConfigurationCode,
                configuration.LoadProfileCode,
                configuration.DuosGroup,
                configuration.MeterPointStatusCode,
                UsageFactors = _scenario.Publisher.Events.OfType<UsageFactorsReceived>().Single().UsageFactors,
                Meters = new[]
                {
                    new ConfiguredMeter
                    {
                        SerialNumber = "32300001",
                        MeterCategoryCode = "RM106",
                        Change = MeterChanges.Retained,
                        Registers =
                        [
                            new ConfiguredRegister
                            {
                                MeterRegisterSequence = "1", TimeslotCode = "00D", RegisterTypeCode = "01", UOMCode = "KWH", MeterMultiplier = 1m,
                                Read = new RegisterRead(12345m, "A", "04", "V", new DateOnly(2026, 3, 24)),
                            },
                        ],
                    },
                },
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsAllThreeAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(3);
}

// UsageFactors is 0..n and NetworksReferenceNumber optional on an MM332: without them the file still configures
// the meter point and lists its meter, and sends no usage-factor event to either reader.
public class WhenInterpretingAnMm332MessageWithoutUsageFactors
{
    private readonly Mm332Scenario _scenario = new(new MarketMessageXmlBuilder().WithoutUsageFactors().WithoutNetworksReferenceNumber());

    [Fact]
    public void PublishesTheMeterConfigurationThenTheChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void SendsSupplyPointsAChangeWithNoUsageFactors() =>
        _scenario.Publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Configuration.UsageFactors.Should().BeEmpty();

    [Fact]
    public void LeavesTheNetworksReferenceNumberEmpty() =>
        _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single().NetworksReferenceNumber.Should().BeNull();

    [Fact]
    public void ReturnsBothAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(2);
}

internal sealed class Mm332Scenario
{
    public const string Mprn = "10012345678";
    public const string Hash = "5b0e3f1c2d4a6b8c9e0f1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f7";
    public const string ReceiptId = "3f2b1c0d-8e7a-4b6c-9d5e-2a1b0c9d8e7f";
    public const string FileName = "DSO_MIM_332_1782352213415_3805739.xml";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    public Mm332Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM332Strategy(Publisher, new FixedTimeProvider(Now), NullLogger<MM332Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("332").WithMprn(Mprn).BuildStream();
        Outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    public RecordingPublisher Publisher { get; } = new();

    public InterpretationOutcome Outcome { get; }

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };

    internal sealed class RecordingPublisher : IEventPublisher
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
