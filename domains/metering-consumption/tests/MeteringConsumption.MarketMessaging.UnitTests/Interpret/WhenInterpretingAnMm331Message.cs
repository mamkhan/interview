using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM331 interpreter over the builder's smart meter put in place: the whole configuration as one change for
// SupplyPoints, keyed by MPRN, and no meter configuration for Profile. Frozen literals from MarketMessageXmlBuilder:
// MPRN 10001331001, MCC12, DUoS DG1, status E, effective 2026-06-24, meter 32300001 (RM106, location 07, exchanged
// reference 000000000032300001) with channels 70 KWT every 30 and 74 KWH every 24; the hash is the receipt's claim.
public class WhenInterpretingAnMm331Message
{
    private static readonly DateOnly EffectiveFrom = new(2026, 6, 24);

    private readonly Mm331Scenario _scenario = new(new MarketMessageXmlBuilder());

    [Fact]
    public void PublishesOnlyTheChangeForSupplyPoints() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void SendsSupplyPointsTheWholeConfigurationAsOneChange() =>
        _scenario.Publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Should().BeEquivalentTo(new
        {
            MPRN = Mm331Scenario.Mprn,
            NetworksReferenceNumber = (string?)null,
            EffectiveFromDate = EffectiveFrom,
            ReadDate = (DateOnly?)null,
            Configuration = new MeterConfiguration
            {
                MeterConfigurationCode = "MCC12",
                DuosGroup = "DG1",
                MeterPointStatusCode = "E",
                UsageFactors = [],
                Meters =
                [
                    new ConfiguredMeter
                    {
                        SerialNumber = "32300001",
                        MeterCategoryCode = "RM106",
                        MeterLocationCode = "07",
                        ExchangedMeterReference = "000000000032300001",
                        Change = MeterChanges.New,
                        Registers =
                        [
                            new ConfiguredRegister { RegisterTypeCode = "70", UOMCode = "KWT", MeteringInterval = 30 },
                            new ConfiguredRegister { RegisterTypeCode = "74", UOMCode = "KWH", MeteringInterval = 24 },
                        ],
                    },
                ],
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());

    [Fact]
    public void ReturnsTheChangeAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(1);
}

internal sealed class Mm331Scenario
{
    public const string Mprn = "10001331001";
    public const string Hash = "7c1e2f3a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f6";
    public const string ReceiptId = "9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d";
    public const string FileName = "DSO_MIM_331_1789650702659_4362280.xml";

    public Mm331Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM331Strategy(Publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 17, 14, 12, 3, TimeSpan.Zero)),
            NullLogger<MM331Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("331").WithMprn(Mprn).BuildStream();
        Outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    public Mm332Scenario.RecordingPublisher Publisher { get; } = new();

    public InterpretationOutcome Outcome { get; }

    private static MarketMessageReceived Receipt() => new()
    {
        EventId = Guid.Parse(ReceiptId),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 17, 14, 12, 0, TimeSpan.Zero),
        CorrelationId = ReceiptId,
        ReceiptId = ReceiptId,
        Market = MeteringConsumptionTopology.Markets.RoiEsb,
        Fuel = MarketMessagingTopology.Fuels.Electricity,
        FileName = FileName,
        ReceivedAt = new DateTimeOffset(2026, 9, 17, 14, 12, 0, TimeSpan.Zero),
        Hash = Hash,
        BlobPath = $"roi-esb/{Hash}.xml",
    };
}
