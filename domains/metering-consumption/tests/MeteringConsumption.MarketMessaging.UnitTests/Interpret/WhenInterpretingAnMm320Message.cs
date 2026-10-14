using MeteringConsumption.Contracts.Events;
using BuildingBlocks.Seedwork;
using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM320 interpreter over the builder's day/night change-of-supplier reading: the meter configuration first, then its
// usage factors, then the whole configuration, the meter in place and its registers included, as one change for
// SupplyPoints, all keyed by MPRN. Frozen literals from MarketMessageXmlBuilder: MPRN 10900000343, read date
// 2026-06-24, configuration NSH, load profile 01, DUOS group DG1, status 1, DAY 2.4100 and NIGHT 1.1300 effective from
// the read date, meter 32300001 (RM106, location 01) with registers 1 (00D, read 12345) and 2 (00N, read 6789), header
// MarketTimestamp 2026-06-25T02:50:10.918Z; the hash is the receipt's claim, not recomputed.
public class WhenInterpretingAnMm320Message
{
    private const string Mprn = "10900000343";
    private const string Hash = "5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724";
    private const string ReceiptId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    private const string FileName = "DSO_MIM_320_1782352213415_3805738.xml";

    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);
    private static readonly DateOnly ReadDate = new(2026, 6, 24);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingAnMm320Message()
    {
        var interpreter = new MM320Strategy(_publisher, new FixedTimeProvider(Now), NullLogger<MM320Strategy>.Instance);
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("320").WithMprn(Mprn).BuildStream();
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    private UsageFactorsReceived UsageFactors => _publisher.Events.OfType<UsageFactorsReceived>().Single();

    [Fact]
    public void PublishesTheConfigurationThenTheUsageFactorsThenTheChange() =>
        _publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(UsageFactorsReceived),
            typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("NSH");
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("1");
        MeterConfiguration.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        MeterConfiguration.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public void DatesTheConfigurationByTheReadingNotAnEffectiveDateItDoesNotHave()
    {
        MeterConfiguration.EffectiveFromDate.Should().BeNull();
        MeterConfiguration.ReadDate.Should().Be(ReadDate);
    }

    [Fact]
    public void IdentifiesTheConfigurationByTheSameDateAsItsReadingId() =>
        MeterConfiguration.EventId.Should().Be(DeterministicGuid.From($"{Hash}:{Mprn}:2026-06-24"));

    [Fact]
    public void PublishesTheUsageFactorsForTheMeterPoint()
    {
        UsageFactors.MPRN.Should().Be(Mprn);
        UsageFactors.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        UsageFactors.ReadDate.Should().Be(ReadDate);
        UsageFactors.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public void CarriesEveryUsageFactorTheFileDeclares() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("00D", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 2.4100m),
            new UsageFactor("00N", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 1.1300m));

    [Fact]
    public void KeysEveryEventByTheMprnSession() =>
        _publisher.Metadata.Should().AllSatisfy(m => m.SessionId.Should().Be(Mprn));

    [Fact]
    public void DerivesEachReadingIdFromTheSealedFile()
    {
        _publisher.Metadata.Select(m => m.MessageId).Should().Equal(
            $"{Hash}:{Mprn}:2026-06-24",
            $"{Hash}:{Mprn}:2026-06-24",
            $"{Hash}:{Mprn}:meter-configuration-change");
        _publisher.Events.Select(e => e.ReadingId).Should().Equal(
            $"{Hash}:{Mprn}:2026-06-24",
            $"{Hash}:{Mprn}:2026-06-24",
            $"{Hash}:{Mprn}:meter-configuration-change");
    }

    [Fact]
    public void PromotesTheMarketOntoEveryMessage() =>
        _publisher.Metadata.Should().AllSatisfy(m => m.ApplicationProperties![MeteringConsumptionTopology.MessageProperties.Market]
            .Should().Be(MeteringConsumptionTopology.Markets.RoiEsb));

    [Fact]
    public void SendsSupplyPointsTheSameConfigurationAsOneChange()
    {
        var configuration = _publisher.Events.OfType<MeterConfigurationReceived>().Single();

        _publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(new
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
                UsageFactors = _publisher.Events.OfType<UsageFactorsReceived>().Single().UsageFactors,
                Meters = new[]
                {
                    new ConfiguredMeter
                    {
                        SerialNumber = "32300001",
                        MeterCategoryCode = "RM106",
                        MeterLocationCode = "01",
                        Change = MeterChanges.InPlace,
                        Registers =
                        [
                            new ConfiguredRegister
                            {
                                MeterRegisterSequence = "1", TimeslotCode = "00D", RegisterTypeCode = "01", UOMCode = "KWH", MeterMultiplier = 1m,
                                PreDecimalDetails = "5", PostDecimalDetails = "0", Read = new RegisterRead(12345m, "A", "04", null, null),
                            },
                            new ConfiguredRegister
                            {
                                MeterRegisterSequence = "2", TimeslotCode = "00N", RegisterTypeCode = "01", UOMCode = "KWH", MeterMultiplier = 1m,
                                PreDecimalDetails = "5", PostDecimalDetails = "0", Read = new RegisterRead(6789m, "A", "04", null, null),
                            },
                        ],
                    },
                },
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsAllThreeAsPublished() => _outcome.ReadingsPublished.Should().Be(3);

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

    private sealed class RecordingPublisher : IEventPublisher
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
