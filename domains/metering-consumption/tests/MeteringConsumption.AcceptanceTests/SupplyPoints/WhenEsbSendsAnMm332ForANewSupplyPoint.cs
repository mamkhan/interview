using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints;

// ESB sends an MM332 for a supply point SupplyPoints has never seen, through the running environment's drop
// container, and SupplyPoints keeps what the file states as history and as the rows in effect today, while
// Consumption's Profile takes the same values as the supply point's latest. The values are MarketMessageXmlBuilder's
// MM332: MCC01, load profile 01, DUoS DG1, status E, effective 2026-06-24, networks reference 2100000012345; usage
// factors 00D estimated 2.41 and actual 2.50 (two entries for one date, so one history row), 00N estimated 1.13.
public sealed class WhenEsbSendsAnMm332ForANewSupplyPoint(EsbSentAnMm332ForANewSupplyPointScenario sent)
    : IClassFixture<EsbSentAnMm332ForANewSupplyPointScenario>
{
    private static readonly DateTime EffectiveFrom = new(2026, 6, 24);

    [Fact]
    public void KeepsTheSupplyPointUnderTheMarketTheFileCameFrom() =>
        sent.SupplyPoint.Market.Should().Be("roi-esb");

    [Fact]
    public void KeepsOneMeterPointRowFromTheEffectiveDate()
    {
        sent.MeterPointHistory.Should().ContainSingle().Which.EffectiveDate.Should().Be(EffectiveFrom);
        sent.MeterPoints.Should().ContainSingle().Which.Should().Be(sent.MeterPointHistory.Single());
    }

    [Fact]
    public void StoresTheFourCodesAsSent()
    {
        var row = sent.MeterPoints.Should().ContainSingle().Subject;

        row.MeterConfigurationCode.Should().Be("MCC01");
        row.LoadProfileCode.Should().Be("01");
        row.DuosGroup.Should().Be("DG1");
        row.MeterPointStatusCode.Should().Be("E");
    }

    [Fact]
    public void KeepsTheMessageTheMeterPointCameFrom()
    {
        var row = sent.MeterPoints.Should().ContainSingle().Subject;

        row.NetworksReferenceNumber.Should().Be("2100000012345");
    }

    [Fact]
    public void KeepsTheDayUsageFactorWithItsEstimateAndItsActual()
    {
        var history = sent.UsageFactorHistory.Should().ContainSingle(factor => factor.TimeslotCode == "00D").Subject;
        var today = sent.UsageFactors.Should().ContainSingle(factor => factor.TimeslotCode == "00D").Subject;

        history.EffectiveDate.Should().Be(EffectiveFrom);
        history.EstimatedUsageFactor.Should().Be(2.41m);
        history.ActualUsageFactor.Should().Be(2.50m);
        today.Should().Be(history);
    }

    [Fact]
    public void KeepsTheNightUsageFactorWithItsEstimateAlone()
    {
        var night = sent.UsageFactors.Should().ContainSingle(factor => factor.TimeslotCode == "00N").Subject;

        night.EffectiveDate.Should().Be(EffectiveFrom);
        night.EstimatedUsageFactor.Should().Be(1.13m);
        night.ActualUsageFactor.Should().BeNull();
    }

    [Fact]
    public void GivesTheProfileTheCodesOfTodaysRow()
    {
        var row = sent.MeterPoints.Should().ContainSingle().Subject;

        (sent.Profile.MeterConfigurationCode, sent.Profile.LoadProfileCode, sent.Profile.DuosGroup, sent.Profile.MeterPointStatusCode)
            .Should().Be((row.MeterConfigurationCode, row.LoadProfileCode, row.DuosGroup, row.MeterPointStatusCode));
        sent.Profile.ConfigurationEffectiveFrom.Should().Be(EffectiveFrom);
    }

    [Fact]
    public void GivesTheProfileTheUsageFactorsOfTodaysRows()
    {
        var day = sent.UsageFactors.Single(factor => factor.TimeslotCode == "00D");
        var night = sent.UsageFactors.Single(factor => factor.TimeslotCode == "00N");

        (sent.Profile.Euf00D, sent.Profile.Auf00D, sent.Profile.Euf00N)
            .Should().Be((day.EstimatedUsageFactor, day.ActualUsageFactor, night.EstimatedUsageFactor));
    }

    [Fact]
    public void RecordsInTheProfileTheSealedFileTheValuesCameFrom()
    {
        sent.Profile.ConfigurationSource.Should().Be(sent.ReceiptHash);
        sent.Profile.Euf00DSource.Should().Be(sent.ReceiptHash);
    }
}
