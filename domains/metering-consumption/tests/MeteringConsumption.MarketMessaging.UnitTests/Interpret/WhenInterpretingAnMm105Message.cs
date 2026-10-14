using System.Text.Json;
using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The MM105 interpreter over the builder's change of supplier: one meter configuration and the same configuration as one change for SupplyPoints, keyed by MPRN, no usage factors,
// and none of the customer's personal data. Frozen literals from MarketMessageXmlBuilder: MPRN 10001105001, MCC12, no
// load profile, DUOS group DG2, status E, effective 2026-06-24, header MarketTimestamp 2026-06-25T02:50:10.918Z; the
// hash is the receipt's claim.
public class WhenInterpretingAnMm105Message
{
    private readonly Mm105Scenario _scenario = new(new MarketMessageXmlBuilder());

    private MeterConfigurationReceived MeterConfiguration => _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mm105Scenario.Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC12");
        MeterConfiguration.LoadProfileCode.Should().BeNull();
        MeterConfiguration.DuosGroup.Should().Be("DG2");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().BeNull();
        MeterConfiguration.OccurredAtUtc.Should().Be(Mm105Scenario.Now);
    }

    [Fact]
    public void DatesTheConfigurationByTheChangeOfSuppliersEffectiveDate()
    {
        MeterConfiguration.EffectiveFromDate.Should().Be(new DateOnly(2026, 6, 24));
        MeterConfiguration.ReadDate.Should().BeNull();
    }

    [Theory]
    [InlineData("Tester")]
    [InlineData("0850000000")]
    [InlineData("test@example.invalid")]
    [InlineData("MAIN STREET")]
    [InlineData("CHURCH ROAD")]
    [InlineData("Test Engineer")]
    [InlineData("SN01")]
    [InlineData("M1")]
    public void CarriesNoneOfTheCustomersPersonalData(string personalValue) =>
        JsonSerializer.Serialize(MeterConfiguration).Should().NotContain(personalValue);

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
                UsageFactors = Array.Empty<UsageFactor>(),
                Meters = Array.Empty<ConfiguredMeter>(),
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsBothAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(2);
}

internal sealed class Mm105Scenario
{
    public const string Mprn = "10001105001";
    public const string Hash = "d0b8f4a7c9e1635b2f8a0d4c6e7b9f1a3d5c7e9a1b2f4d6c8e0a3b5d7f9c1e46";
    public const string ReceiptId = "7c5f3d2e-4a6b-4c8d-0e1f-2a3b4c5d6e7f";
    public const string FileName = "DSO_MIM_105_1782352213415_3805747.xml";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    public Mm105Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM105Strategy(Publisher, new FixedTimeProvider(Now), NullLogger<MM105Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("105").WithMprn(Mprn).BuildStream();
        Outcome = interpreter.InterpretAsync(Receipt(), payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    public Mm332Scenario.RecordingPublisher Publisher { get; } = new();

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
}
