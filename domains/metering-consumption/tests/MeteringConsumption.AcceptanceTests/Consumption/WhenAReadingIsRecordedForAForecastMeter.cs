using System.Text.Json;
using AwesomeAssertions;
using MeteringConsumption.AcceptanceTests.Consumption.Scenarios;

namespace MeteringConsumption.AcceptanceTests.Consumption;

// Eight days of 12 kWh reach the running environment (ReadingsRecordedForAForecastMeterScenario); the last one's forecast
// is current. Seven days after the 06-13 baseline give 84 kWh on each of 48 tiers in total, over the LP1 window sum
// 0.0171328945; the expected values were derived offline from the real LP1 curve, each day rounded to six decimals.
public sealed class WhenAReadingIsRecordedForAForecastMeter(ReadingsRecordedForAForecastMeterScenario scenario)
    : IClassFixture<ReadingsRecordedForAForecastMeterScenario>
{
    [Fact]
    public void ForecastsEachReadingAsTheNextVersion() =>
        scenario.Forecast.ForecastVersion.Should().Be(ReadingsRecordedForAForecastMeterScenario.ReadDays);

    [Fact]
    public void ForecastsSixtyTwoDaysFromTheDayAfterTheLatestRead()
    {
        scenario.Forecast.AnchorDate.Should().Be(new DateTime(2026, 6, 21));
        scenario.Forecast.HorizonDays.Should().Be(62);
    }

    [Fact]
    public void ForecastsFromTheMetersOwnUse()
    {
        var daily = JsonSerializer.Deserialize<decimal[]>(scenario.Forecast.Daily)!;

        scenario.Forecast.UsageFactorSource.Should().Be("PUF");
        daily[0].Should().Be(12.673680m);
        daily[61].Should().Be(11.632656m);
        scenario.Forecast.TotalKwh.Should().Be(731.223114m);
    }

    [Fact]
    public void RecordsWhatSupplyPointsAndTheProfileStated()
    {
        using var inputs = JsonDocument.Parse(scenario.Forecast.Inputs);
        var root = inputs.RootElement;

        root.GetProperty("configurationDay").GetString().Should().Be("2026-06-21");
        root.GetProperty("configuration").GetProperty("meterConfigurationCode").GetString().Should().Be("MCC01");
        root.GetProperty("tariff").GetProperty("facts").GetProperty("isNsmp").GetBoolean().Should().BeTrue();
        root.GetProperty("profile").GetProperty("curveLoadProfile").GetString().Should().Be("LP1");
    }
}
