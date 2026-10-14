using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// An MCC1 meter on LP1 with 12 kWh a day read up to 2026-04-30. Ten days leave nine counted after the excluded
// baseline, 108 kWh over the LP1 window sum 0.0242478942; three days are too few for an own-use factor, so the DG1
// average of 3,853 kWh applies. Expected values are the proof of concept's Native literals for the same inputs.
public sealed class WhenForecastingASingleRateMeter
{
    private static readonly DateOnly LatestReadDate = new(2026, 4, 30);

    private static ForecastOutcome Forecast(int readDays) => ForecastCalculation.Calculate(
        new ConsumptionWindowBuilder().DailyUse(LatestReadDate, readDays, 12m).Build(),
        ForecastPersonas.SingleRate,
        ForecastPersonas.NotTimeOfUse,
        V1LoadProfiles.Curve,
        ForecastPersonas.Dg1In2026,
        ForecastPersonas.Settings);

    [Fact]
    public void ForecastsFromTheMetersOwnUse()
    {
        var result = ((ForecastOutcome.Computed)Forecast(10)).Result;

        result.Factors.Source.Should().Be(UsageFactorSource.Puf);
        result.Factors.Numerators.Should().Equal(108m);
        result.Factors.Denominators.Should().Equal(0.0242478942m);
    }

    [Fact]
    public void StartsTheDayAfterTheLatestReadAndRunsSixtyTwoDays()
    {
        var result = ((ForecastOutcome.Computed)Forecast(10)).Result;

        result.AnchorDate.Should().Be(new DateOnly(2026, 5, 1));
        result.Days.Should().HaveCount(62);
        result.Days[^1].Date.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void ForecastsEachDayAtFullPrecision()
    {
        var result = ((ForecastOutcome.Computed)Forecast(10)).Result;

        result.Days[0].Kwh.Should().Be(11.037274915196553439267315840m);
        result.Days[61].Kwh.Should().Be(10.539720467767464937223290920m);
    }

    [Fact]
    public void FallsBackToTheAreaAverageOnAShortHistory()
    {
        var result = ((ForecastOutcome.Computed)Forecast(3)).Result;

        result.Factors.Source.Should().Be(UsageFactorSource.Aau);
        result.Days[0].Kwh.Should().Be(9.5479721154m);
        result.Days[61].Kwh.Should().Be(9.1175546413m);
    }
}
