using AwesomeAssertions;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;
using static MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios.SealedMessagesInterpretedScenario;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// What only the bus can show about interpreting a file: each event reaches its own topic, keyed on the supply point's
// session and carrying the receipt as correlation, and its body reads back as its contract. Which strategy a type
// reaches and every value a strategy states are unit tests; the values here are a few per contract, enough to prove
// the round trip. The real extract's frozen values are WhenParsingARealEsbIntervalFile's; the MM332's are the
// builder's: MCC01, load profile 01, DG1, status E, effective 2026-06-24, retained meter 32300001 with register 1 read
// 12345.
public sealed class WhenSealedMessagesAreInterpreted(SealedMessagesInterpretedScenario interpreted)
    : IClassFixture<SealedMessagesInterpretedScenario>
{
    private static readonly DateOnly EffectiveFrom = new(2026, 6, 24);

    [Fact]
    public void PublishesOneProcessedOutcomePerFileUnderItsReceiptId()
    {
        interpreted.Outcomes[ExtractReceiptId].Should().BeEquivalentTo(
            new { MessageType = "343", Outcome = MarketMessageOutcomes.Processed, Interpreter = "MM343Strategy", ReadingsPublished = 2 });
        interpreted.Outcomes[ConfigurationReceiptId].Should().BeEquivalentTo(
            new { MessageType = "332", Outcome = MarketMessageOutcomes.Processed, Interpreter = "MM332Strategy", ReadingsPublished = 3 });
        interpreted.OutcomeMessageIds.Should().OnlyContain(pair => pair.Key == pair.Value);
    }

    [Fact]
    public void PublishesTheExtractsReadingsOnTheirSupplyPointsSessions()
    {
        interpreted.Readings.Should().AllSatisfy(r =>
        {
            r.Message.SessionId.Should().Be(r.Reading.MPRN);
            r.Message.MessageId.Should().Be(r.Reading.ReadingId);
            r.Message.CorrelationId.Should().Be(ExtractReceiptId);
        });
        var first = interpreted.Readings.Single(r => r.Reading.MPRN == "10900000343").Reading;
        var second = interpreted.Readings.Single(r => r.Reading.MPRN == "10900001343").Reading;
        first.Periods[0].Value.Should().Be(0.14m);
        first.Periods.Sum(p => p.Value).Should().Be(17.441m);
        second.Periods[0].Value.Should().Be(0.069m);
        second.Periods.Sum(p => p.Value).Should().Be(6.373m);
    }

    [Fact]
    public void PublishesEachConfigurationEventOnItsOwnTopicOnTheSupplyPointsSession() =>
        interpreted.Configuration.Values.Should().HaveCount(3).And.AllSatisfy(message =>
        {
            message.SessionId.Should().Be(ConfigurationMprn);
            message.CorrelationId.Should().Be(ConfigurationReceiptId);
        });

    [Fact]
    public void ReadsBackTheProfilesCopyAsItsContracts()
    {
        var configuration = interpreted.Body<MeterConfigurationReceived>(MeteringConsumptionTopology.Topics.MeterConfigurationReceived);
        (configuration.MPRN, configuration.MeterConfigurationCode, configuration.EffectiveFromDate)
            .Should().Be((ConfigurationMprn, "MCC01", (DateOnly?)EffectiveFrom));
        interpreted.Body<UsageFactorsReceived>(MeteringConsumptionTopology.Topics.UsageFactorsReceived).UsageFactors.Should().Equal(
            new UsageFactor("00D", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 2.4100m),
            new UsageFactor("00D", EffectiveFrom, ActualUsageFactor: 2.5000m, EstimatedUsageFactor: null),
            new UsageFactor("00N", EffectiveFrom, ActualUsageFactor: null, EstimatedUsageFactor: 1.1300m));
    }

    [Fact]
    public void ReadsBackTheWholeConfigurationAsOneChange()
    {
        var change = interpreted.Body<MeterConfigurationChangeReceived>(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived);
        (change.MPRN, change.EffectiveFromDate, change.Configuration.MeterConfigurationCode, change.Configuration.DuosGroup)
            .Should().Be((ConfigurationMprn, (DateOnly?)EffectiveFrom, "MCC01", "DG1"));
        change.Configuration.UsageFactors.Should().HaveCount(3);
        var meter = change.Configuration.Meters.Should().ContainSingle().Subject;
        (meter.SerialNumber, meter.Change).Should().Be(("32300001", MeterChanges.Retained));
        meter.Registers.Should().ContainSingle().Which.Read!.ReadingValue.Should().Be(12345m);
    }
}
