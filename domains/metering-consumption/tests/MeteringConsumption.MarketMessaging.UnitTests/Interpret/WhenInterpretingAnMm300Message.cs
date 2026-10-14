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

// The MM300 interpreter over the builder's scheduled reading: the meter configuration first, then its usage factors,
// then the same configuration as one change for SupplyPoints, all keyed by MPRN. Frozen literals from MarketMessageXmlBuilder: MPRN 10001300001, no configuration code, load
// profile 01, DUOS group DG1, status 1, read date 2026-06-24, DAY 2.4100 and NIGHT 1.1300 estimated, header
// MarketTimestamp 2026-06-25T02:50:10.918Z; the hash is the receipt's claim.
public class WhenInterpretingAnMm300Message
{
    private static readonly DateOnly ReadDate = new(2026, 6, 24);

    private readonly Mm300Scenario _scenario = new(new MarketMessageXmlBuilder());

    private MeterConfigurationReceived MeterConfiguration => _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesTheMeterConfigurationThenTheUsageFactorsThenTheChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(UsageFactorsReceived),
            typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheMeterPointsConfiguration()
    {
        MeterConfiguration.MPRN.Should().Be(Mm300Scenario.Mprn);
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.MeterPointStatusCode.Should().Be("1");
        MeterConfiguration.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        MeterConfiguration.OccurredAtUtc.Should().Be(Mm300Scenario.Now);
    }

    [Fact]
    public void LeavesTheMeterConfigurationCodeItDoesNotCarryEmpty() =>
        MeterConfiguration.MeterConfigurationCode.Should().BeNull();

    [Fact]
    public void DatesTheConfigurationByTheReadingNotAnEffectiveDateItDoesNotHave()
    {
        MeterConfiguration.EffectiveFromDate.Should().BeNull();
        MeterConfiguration.ReadDate.Should().Be(ReadDate);
    }

    [Fact]
    public void CarriesEveryUsageFactorTheFileDeclaresDatedByTheReading()
    {
        var usageFactors = _scenario.Publisher.Events.OfType<UsageFactorsReceived>().Single();

        usageFactors.MPRN.Should().Be(Mm300Scenario.Mprn);
        usageFactors.ReadDate.Should().Be(ReadDate);
        usageFactors.UsageFactors.Should().Equal(
            new UsageFactor("00D", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 2.4100m),
            new UsageFactor("00N", ReadDate, ActualUsageFactor: null, EstimatedUsageFactor: 1.1300m));
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
                Meters = Array.Empty<ConfiguredMeter>(),
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsAllThreeAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(3);
}

// UsageFactors is 0..n on an MM300: without them the file still configures the meter point, and sends no
// usage-factor event.
public class WhenInterpretingAnMm300MessageWithoutUsageFactors
{
    private readonly Mm300Scenario _scenario = new(new MarketMessageXmlBuilder().WithoutUsageFactors());

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void SendsSupplyPointsAChangeWithNoUsageFactors() =>
        _scenario.Publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Configuration.UsageFactors.Should().BeEmpty();

    [Fact]
    public void ReturnsBothAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(2);
}

internal sealed class Mm300Scenario
{
    public const string Mprn = "10001300001";
    public const string Hash = "c3a1e7f0b2d4968a5e1f3c7b9d0a2e4f6b8c1d3e5f7a9b0c2d4e6f8a1b3c5d7e";
    public const string ReceiptId = "6a4d2f1e-3b5c-4e7a-8f9b-1c2d3e4f5a6b";
    public const string FileName = "DSO_MIM_300_1782352213415_3805740.xml";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    public Mm300Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM300Strategy(Publisher, new FixedTimeProvider(Now), NullLogger<MM300Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("300").WithMprn(Mprn).BuildStream();
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
