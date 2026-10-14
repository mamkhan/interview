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

// The MM332 interpreter over a real ESB meter exchange, a 24-hour meter removed and a smart meter fitted, its
// identifiers replaced by fake ones and every other value as ESB sent it. ESB states the estimate and the actual on
// separate entries with their own dates. Expected values read offline with Python's xml.etree, sharing no code with
// the parser: MCC01, load profile 01, DUOS group DG1, status E, effective 2026-09-25, 24H estimated 5033.03824582
// from 2026-09-25 and actual 4276.94863062 from 2026-07-30, new meter 90003322 (RM107, location 07) with register 001
// (24H, type 01, KWH, multiplier 1, pre-decimal 07, post-decimal 0, read 0 type A reason 21), removed meter 90003321
// left to the exchange, header MarketTimestamp 2026-10-01T09:55:35.891Z; sha256
// of the file 590fbd6de8812259feb0fd43e0fae7737029000104a6ad979ae06bdfd4f6f785 (shasum -a 256).
public class WhenInterpretingARealMm332File
{
    private const string FileName = "ESB_MM332_10900000332_0000000790000332.xml";
    private const string Mprn = "10900000332";
    private const string Hash = "590fbd6de8812259feb0fd43e0fae7737029000104a6ad979ae06bdfd4f6f785";
    private const string ReceiptId = "b4a09e6d-27f1-4c38-9d52-71e0c3f8a5b9";

    private static readonly DateOnly EffectiveFrom = new(2026, 9, 25);

    private readonly RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingARealMm332File()
    {
        var interpreter = new MM332Strategy(_publisher, new FixedTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 3, TimeSpan.Zero)),
                                            NullLogger<MM332Strategy>.Instance);
        using var payload = File.OpenRead($"samples/{FileName}");
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    private UsageFactorsReceived UsageFactors => _publisher.Events.OfType<UsageFactorsReceived>().Single();

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC01");
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().Be("019000000332");
    }

    [Fact]
    public void DatesTheConfigurationByItsEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(EffectiveFrom);
        MeterConfiguration.ReadDate.Should().BeNull();
    }

    [Fact]
    public void CarriesTheEstimateAndTheActualAsSeparateFactorsWithTheirOwnDates() =>
        UsageFactors.UsageFactors.Should().Equal(
            new UsageFactor("24H", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 5033.03824582m),
            new UsageFactor("24H", new DateOnly(2026, 7, 30), ActualUsageFactor: 4276.94863062m, EstimatedUsageFactor: null));

    [Fact]
    public void DatesTheUsageFactorsByTheConfigurationsEffectiveDate() => UsageFactors.ReadDate.Should().Be(EffectiveFrom);

    [Fact]
    public void SendsSupplyPointsTheWholeConfigurationWithOnlyTheNewMeterAsOneEvent() =>
        _publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(new
        {
            MPRN = Mprn,
            NetworksReferenceNumber = "019000000332",
            EffectiveFromDate = EffectiveFrom,
            ReadDate = (DateOnly?)null,
            ReadingId = $"{Hash}:{Mprn}:meter-configuration-change",
            Configuration = new MeterConfiguration
            {
                MeterConfigurationCode = "MCC01",
                LoadProfileCode = "01",
                DuosGroup = "DG1",
                MeterPointStatusCode = "E",
                UsageFactors =
                [
                    new UsageFactor("24H", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 5033.03824582m),
                    new UsageFactor("24H", new DateOnly(2026, 7, 30), ActualUsageFactor: 4276.94863062m, EstimatedUsageFactor: null),
                ],
                Meters =
                [
                    new ConfiguredMeter
                    {
                        SerialNumber = "90003322",
                        MeterCategoryCode = "RM107",
                        MeterLocationCode = "07",
                        Change = MeterChanges.New,
                        Registers =
                        [
                            new ConfiguredRegister
                            {
                                MeterRegisterSequence = "001", TimeslotCode = "24H", RegisterTypeCode = "01", UOMCode = "KWH", MeterMultiplier = 1m,
                                PreDecimalDetails = "07", PostDecimalDetails = "0", Read = new RegisterRead(0m, "A", "21", null, null),
                            },
                        ],
                    },
                ],
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());

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
