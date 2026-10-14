using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.IntegrationTests.Scenarios;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The Profile role booted through the module's own registration, each of its consumers driven once on its production
// subscription, and the supply point's one row holding all four groups. Frozen values are the builders' agreed
// examples (MCC01, DG1, effective 2026-09-21 from file 801a3bd1…fc25; the V1 reader's run of 2026-10-07 at 04:00 UTC)
// and two factors, one with eight decimals. Which statement wins when two meet is the unit tests'.
public sealed class WhenProfileStatementsArriveOnTheBus(ProfileStatementsArrivedOnTheBusScenario arrived)
    : IClassFixture<ProfileStatementsArrivedOnTheBusScenario>
{
    [Fact]
    public void StoresTheCodesTheMarketMessageStates() =>
        arrived.Profile!.Configuration.Should().Be(new MeterConfigurationStatement
        {
            MeterConfigurationCode = "MCC01",
            LoadProfileCode = "01",
            DuosGroup = "DG1",
            MeterPointStatusCode = "E",
            EffectiveFrom = new DateOnly(2026, 9, 21),
            Source = "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25",
            MessageType = "332",
        });

    [Fact]
    public void StoresTheSmartMeterFlagWithWhenItWasReadAndWhereItCameFrom() =>
        arrived.Profile!.SmartMeterFlag.Should().Be(
            new SmartMeterFlagStatement(true, new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero), "v1-reader:2026-10-07:1"));

    [Fact]
    public void StoresEachUsageFactorWithAllItsDecimals()
    {
        arrived.Profile!.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Actual)].Value.Should().Be(3842.51730123m);
        arrived.Profile.UsageFactors[new UsageFactorSlot("00N", UsageFactorKind.Estimated)].Value.Should().Be(1250.5m);
    }

    [Fact]
    public void StoresTheTariffValuesFromGrowthsTopicWithWhenAndWhoReadThem() =>
        arrived.Profile!.Tariff.Should().Be(TariffStatement.Of(
            true, "TwentyFourHour", "LP1", new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero), "v1-reader/tariff-values"));
}
