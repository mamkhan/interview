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

// The MM101 interpreter over the builder's new connection: one meter configuration and the same configuration as one change for SupplyPoints, keyed by MPRN, no usage factors,
// and none of the customer's personal data. Frozen literals from MarketMessageXmlBuilder: MPRN 10001101001, MCC02, load
// profile 02, DUOS group DG1, status E, effective 2026-06-24, header MarketTimestamp 2026-06-25T02:50:10.918Z; the hash
// is the receipt's claim.
public class WhenInterpretingAnMm101Message
{
    private readonly Mm101Scenario _scenario = new(new MarketMessageXmlBuilder());

    private MeterConfigurationReceived MeterConfiguration => _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mm101Scenario.Mprn);
        MeterConfiguration.MeterConfigurationCode.Should().Be("MCC02");
        MeterConfiguration.LoadProfileCode.Should().Be("02");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("E");
        MeterConfiguration.NetworksReferenceNumber.Should().BeNull();
        MeterConfiguration.OccurredAtUtc.Should().Be(Mm101Scenario.Now);
    }

    [Fact]
    public void DatesTheConfigurationByItsEffectiveDateNotTheRegistrationsReceipt()
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

// EffectiveFromDate is optional on an MM101 and RegistrationReceiptDate is not when the configuration applies, so a
// file without one sends nothing rather than a configuration with an invented date.
public class WhenInterpretingAnMm101MessageWithoutAnEffectiveDate
{
    private readonly Mm101Scenario _scenario = new(new MarketMessageXmlBuilder().WithoutEffectiveFromDate());

    [Fact]
    public void PublishesNothing() => _scenario.Publisher.EventTypes.Should().BeEmpty();

    [Fact]
    public void ReturnsNoneAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(0);
}

internal sealed class Mm101Scenario
{
    public const string Mprn = "10001101001";
    public const string Hash = "c9a7e3f6b8d0524a1e7f9c3b5d6a8e0f2c4b6d8f0a1e3c5b7d9f2a4c6e8b0d35";
    public const string ReceiptId = "6b4e2c1d-3f5a-4b7c-9d0e-1f2a3b4c5d6e";
    public const string FileName = "DSO_MIM_101_1782352213415_3805746.xml";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    public Mm101Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM101Strategy(Publisher, new FixedTimeProvider(Now), NullLogger<MM101Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("101").WithMprn(Mprn).BuildStream();
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
