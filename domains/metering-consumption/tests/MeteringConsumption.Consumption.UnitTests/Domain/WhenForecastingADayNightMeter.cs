using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// An MCC2 meter on LP2 using 4 kWh a night and 8 kWh a day, read up to 2026-04-30: ten days give band totals of 36
// and 72 kWh over nine counted days; three days fall back to DG1's night and day averages, 1,966 and 2,945 kWh.
// Expected values are the proof of concept's Native literals for the same inputs.
public sealed class WhenForecastingADayNightMeter
{
    private static readonly DateOnly LatestReadDate = new(2026, 4, 30);

    private static ForecastOutcome Forecast(int readDays, decimal nightKwh = 4m) => ForecastCalculation.Calculate(
        new ConsumptionWindowBuilder().DailyUse(LatestReadDate, readDays, nightKwh, 8m).Build(),
        ForecastPersonas.DayNight,
        ForecastPersonas.NotTimeOfUse,
        V1LoadProfiles.Curve,
        ForecastPersonas.Dg1In2026,
        ForecastPersonas.Settings);

    [Fact]
    public void ScalesTheNightAndDayTiersByTheirOwnUse()
    {
        var result = ((ForecastOutcome.Computed)Forecast(10)).Result;

        result.Factors.Source.Should().Be(UsageFactorSource.Puf);
        result.Factors.Numerators.Should().Equal(36m, 72m);
        result.Days[0].Kwh.Should().Be(9.881452714187453434286589692m);
    }

    [Fact]
    public void FallsBackToTheNightAndDayAreaAveragesOnAShortHistory()
    {
        var result = ((ForecastOutcome.Computed)Forecast(3)).Result;

        result.Factors.Source.Should().Be(UsageFactorSource.Aau);
        result.Factors.PerTier.Should().Equal(1966m, 2945m);
        result.Days[0].Kwh.Should().Be(11.9969946808m);
    }

    [Fact]
    public void TakesTheAreaAverageWhenOneBandUsedNothing()
    {
        var result = ((ForecastOutcome.Computed)Forecast(10, nightKwh: 0m)).Result;

        result.Factors.Source.Should().Be(UsageFactorSource.Aau);
    }
}
