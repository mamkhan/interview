using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints;

// The V1 reader's load of one supply point reaches SupplyPoints through the platform's topic and Reload's subscription in
// the running environment: the builder's values, MCC01, 01, DG1 from 2026-08-03, the 24H estimate 4127.5 from 2026-08-03
// and the 24H actual 3980.25 from 2026-09-01, with no meter point status and no networks reference number.
public sealed class WhenV1LoadsASupplyPoint(V1LoadedASupplyPointScenario loaded) : IClassFixture<V1LoadedASupplyPointScenario>
{
    [Fact]
    public void StoresTheCodesFromV1sDate() =>
        loaded.MeterPoints.Should().Equal(new EsbSentAnMm332ForANewSupplyPointScenario.MeterPointRow
        {
            EffectiveDate = new DateTime(2026, 8, 3),
            MeterConfigurationCode = "MCC01",
            LoadProfileCode = "01",
            DuosGroup = "DG1",
        });

    [Fact]
    public void KeepsEachUsageFactorAtItsOwnDateAndTodaysRowWithBoth()
    {
        loaded.UsageFactorHistory.Should().Equal(
            new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
            {
                TimeslotCode = "24H", EffectiveDate = new DateTime(2026, 8, 3), EstimatedUsageFactor = 4127.5m,
            },
            new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
            {
                TimeslotCode = "24H", EffectiveDate = new DateTime(2026, 9, 1), ActualUsageFactor = 3980.25m,
            });
        loaded.UsageFactors.Should().Equal(new EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow
        {
            TimeslotCode = "24H", EffectiveDate = new DateTime(2026, 9, 1), EstimatedUsageFactor = 4127.5m, ActualUsageFactor = 3980.25m,
        });
    }
}
