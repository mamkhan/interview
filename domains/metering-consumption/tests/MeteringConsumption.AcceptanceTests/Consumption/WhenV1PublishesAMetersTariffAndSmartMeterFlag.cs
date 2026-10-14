using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.Consumption.Scenarios;

namespace MeteringConsumption.AcceptanceTests.Consumption;

// The V1 reader's nightly values for one supply point are published into the running environment, and the Profile
// feature stores both in the supply point's row: the tariff from Growth's topic, the smart meter flag from the meter
// configuration topic. The values are the builders' defaults, the reader's run of 2026-10-07: a 48-rate tariff on
// the 24-hour curve with load profile LP1, and a smart meter.
public sealed class WhenV1PublishesAMetersTariffAndSmartMeterFlag(V1PublishedTariffAndSmartMeterFlagScenario published)
    : IClassFixture<V1PublishedTariffAndSmartMeterFlagScenario>
{
    [Fact]
    public void StoresTheTariffValues()
    {
        published.Row.IsTou.Should().BeTrue();
        published.Row.TariffCalcType.Should().Be("TwentyFourHour");
        published.Row.EffectiveLoadProfileCode.Should().Be("LP1");
    }

    [Fact]
    public void StoresTheSmartMeterFlag() => published.Row.IsNsmp.Should().BeTrue();
}
