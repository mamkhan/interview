using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.SupplyPoints.IntegrationTests.Scenarios;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// SupplyPoints holds every ESB value Consumption's Profile uses, with the same latest value. Derived by hand from the
// scenario's messages: MCC02 (2026-10-01) over MCC01, load profile 03 (the 2026-10-01 correction), DG1 (the late DG2 of
// 2026-08-01 sits before 2026-09-21 and changes nothing latest), status E; 24H estimate 3910.0, actual 3842.5173.
public sealed class WhenProfileAndStoreReadTheSameMessages(ProfileAndStoreReadTheSameMessagesScenario read)
    : IClassFixture<ProfileAndStoreReadTheSameMessagesScenario>
{
    [Fact]
    public void BothHoldTheSameLatestMeterPointCodes()
    {
        var profile = read.Profile!.Configuration!;

        (profile.MeterConfigurationCode, profile.LoadProfileCode, profile.DuosGroup, profile.MeterPointStatusCode)
            .Should().Be(("MCC02", "03", "DG1", "E"));
        read.TodaysMeterPoint.Should().Be(("MCC02", "03", "DG1", "E"));
    }

    [Fact]
    public void BothHoldTheSameLatestUsageFactors()
    {
        read.Profile!.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Estimated)].Value.Should().Be(3910.0m);
        read.Profile.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Actual)].Value.Should().Be(3842.5173m);
        read.Todays24HUsageFactor!.Value.Estimated.Should().Be(3910.0m);
        read.Todays24HUsageFactor.Value.Actual.Should().Be(3842.5173m);
    }
}
