using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints;

// The real MM332 for 10900000332 lists the 24H estimate 5033.03824582 from 2026-09-25 before the 24H actual
// 4276.94863062 from 2026-07-30 (read off the file). History keeps each statement's own value at its own date, and
// today's 24H row, 2026-09-25's, carries the actual stated from 2026-07-30 as well as the estimate.
public sealed class WhenEsbSendsTheRealMm332(EsbSentTheRealMm332Scenario sent) : IClassFixture<EsbSentTheRealMm332Scenario>
{
    [Fact]
    public void KeepsEach24HStatementAsHistoryAtItsOwnDate() =>
        sent.UsageFactorHistory.Should().Equal(
            new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
            {
                TimeslotCode = "24H", EffectiveDate = new DateTime(2026, 7, 30), ActualUsageFactor = 4276.94863062m,
            },
            new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
            {
                TimeslotCode = "24H", EffectiveDate = new DateTime(2026, 9, 25), EstimatedUsageFactor = 5033.03824582m,
            });

    [Fact]
    public void GivesTodays24HRowTheActualStatedFromBeforeIt() =>
        sent.UsageFactors.Should().Equal(new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
        {
            TimeslotCode = "24H",
            EffectiveDate = new DateTime(2026, 9, 25),
            EstimatedUsageFactor = 5033.03824582m,
            ActualUsageFactor = 4276.94863062m,
        });
}
