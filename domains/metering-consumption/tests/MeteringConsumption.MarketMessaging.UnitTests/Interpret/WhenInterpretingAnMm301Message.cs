using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM301 interpreter over the builder's changed characteristics: one meter configuration and the same configuration as one change for SupplyPoints, keyed by MPRN, and no
// usage factors, which a 301 never carries. Frozen literals from MarketMessageXmlBuilder: MPRN 10001301001, MCC12,
// load profile 01, DUOS group DG1, no status, effective 2026-06-24, header MarketTimestamp 2026-06-25T02:50:10.918Z;
// the hash is the receipt's claim.
public class WhenInterpretingAnMm301Message
{
    private const string Mprn = "10001301001";
    private const string Hash = "b8f6d2e5a7c9413f0d6e8b2a4c5f7d9e1b3a5c7e9f0d2b4a6c8e1f3d5b7a9c24";
    private const string ReceiptId = "5a3d1b0c-2e4f-4a6b-8c9d-0e1f2a3b4c5d";
    private const string FileName = "DSO_MIM_301_1782352213415_3805745.xml";

    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    private readonly Mm332Scenario.RecordingPublisher _publisher = new();
    private readonly InterpretationOutcome _outcome;

    public WhenInterpretingAnMm301Message()
    {
        var interpreter = new MM301Strategy(_publisher, new FixedTimeProvider(Now), NullLogger<MM301Strategy>.Instance);
        using var payload = new MarketMessageXmlBuilder().WithMessageTypeCode("301").WithMprn(Mprn).BuildStream();
        _outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    private MeterConfigurationReceived MeterConfiguration => _publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheCodesTheFileStates()
    {
        MeterConfiguration.MPRN.Should().Be(Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC12");
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public void LeavesTheStatusAndNetworksReferenceItDoesNotCarryEmpty()
    {
        MeterConfiguration.MeterPointStatusCode.Should().BeNull();
        MeterConfiguration.NetworksReferenceNumber.Should().BeNull();
    }

    [Fact]
    public void DatesTheConfigurationByItsEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
        MeterConfiguration.ReadDate.Should().BeNull();
    }

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
                UsageFactors = Array.Empty<UsageFactor>(),
                Meters = Array.Empty<ConfiguredMeter>(),
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsBothAsPublished() => _outcome.ReadingsPublished.Should().Be(2);

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
}
