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

// The MM306 interpreter over the builder's de-energisation: the meter configuration first, then its usage factors,
// then the same configuration as one change for SupplyPoints, all keyed by MPRN. Frozen literals from MarketMessageXmlBuilder: MPRN 10001306001, no configuration code, load
// profile 01, DUOS group DG1, status DR, effective 2026-06-24, DAY 2.4100 and NIGHT 1.1300 estimated, header
// MarketTimestamp 2026-06-25T02:50:10.918Z; the hash is the receipt's claim.
public class WhenInterpretingAnMm306Message
{
    private static readonly DateOnly EffectiveFrom = new(2026, 6, 24);

    private readonly Mm306Scenario _scenario = new(new MarketMessageXmlBuilder());

    private MeterConfigurationReceived MeterConfiguration => _scenario.Publisher.Events.OfType<MeterConfigurationReceived>().Single();

    [Fact]
    public void PublishesTheMeterConfigurationThenTheUsageFactorsThenTheChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(UsageFactorsReceived),
            typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void CarriesTheDeEnergisedStatus()
    {
        MeterConfiguration.MPRN.Should().Be(Mm306Scenario.Mprn);
        MeterConfiguration.MeterPointStatusCode.Should().Be("DR");
        MeterConfiguration.LoadProfileCode.Should().Be("01");
        MeterConfiguration.DuosGroup.Should().Be("DG1");
        MeterConfiguration.NetworksReferenceNumber.Should().Be(MarketMessageXmlBuilder.NetworksReferenceNumber);
        MeterConfiguration.OccurredAtUtc.Should().Be(Mm306Scenario.Now);
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
    public void CarriesEveryUsageFactorTheFileDeclaresDatedByTheEffectiveDate()
    {
        var usageFactors = _scenario.Publisher.Events.OfType<UsageFactorsReceived>().Single();

        usageFactors.MPRN.Should().Be(Mm306Scenario.Mprn);
        usageFactors.ReadDate.Should().Be(EffectiveFrom);
        usageFactors.UsageFactors.Should().Equal(
            new UsageFactor("00D", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 2.4100m),
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
                Meters = Array.Empty<ConfiguredMeter>(),
            },
        }, options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ReturnsAllThreeAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(3);
}

// UsageFactors is 0..n on an MM306: without them the file still records the status change, and sends no usage-factor
// event.
public class WhenInterpretingAnMm306MessageWithoutUsageFactors
{
    private readonly Mm306Scenario _scenario = new(new MarketMessageXmlBuilder().WithoutUsageFactors());

    [Fact]
    public void PublishesOnlyTheMeterConfigurationAndSupplyPointsChange() =>
        _scenario.Publisher.EventTypes.Should().Equal(typeof(MeterConfigurationReceived), typeof(MeterConfigurationChangeReceived));

    [Fact]
    public void SendsSupplyPointsAChangeWithNoUsageFactors() =>
        _scenario.Publisher.Events.OfType<MeterConfigurationChangeReceived>().Single().Configuration.UsageFactors.Should().BeEmpty();

    [Fact]
    public void ReturnsBothAsPublished() => _scenario.Outcome.ReadingsPublished.Should().Be(2);
}

internal sealed class Mm306Scenario
{
    public const string Mprn = "10001306001";
    public const string Hash = "e5c3a9b2d4f6180c7a3b5e9d1f2c4a6b8d0e2f4a6c8b1d3e5f7a9c0b2d4e6f81";
    public const string ReceiptId = "8c6f4b3a-5d7e-4a9c-8b1d-3e4f5a6b7c8d";
    public const string FileName = "DSO_MIM_306_1782352213415_3805742.xml";

    public static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    public Mm306Scenario(MarketMessageXmlBuilder builder)
    {
        var interpreter = new MM306Strategy(Publisher, new FixedTimeProvider(Now), NullLogger<MM306Strategy>.Instance);
        using var payload = builder.WithMessageTypeCode("306").WithMprn(Mprn).BuildStream();
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
