using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// An MCC12 NSMP meter on a 24-hour tariff with LP1, read up to 2026-04-30. With no usable usage factor it gets V1's
// 62 days of zero, marked as such; with a curve the reference data cannot cover it gets no forecast at all. The LP1
// extract runs to 2026-09-27, so a horizon from 2026-09-02 runs past it.
public sealed class WhenAMeterHasNoUsableForecast
{
    private static ForecastOutcome Forecast(
        DateOnly latestReadDate, int readDays, TariffFacts tariff, AnnualAverageUsage? annualAverage = null) =>
        ForecastCalculation.Calculate(
            new ConsumptionWindowBuilder().DailyUse(latestReadDate, readDays, 12m).Build(),
            ForecastPersonas.TimeOfUse("MCC12", "01"),
            tariff,
            V1LoadProfiles.Curve,
            annualAverage,
            ForecastPersonas.Settings);

    [Fact]
    public void ForecastsSixtyTwoDaysOfZeroMarkedAsHavingNoUsageFactor()
    {
        var outcome = Forecast(new DateOnly(2026, 4, 30), 3, ForecastPersonas.NsmpTwentyFourHour("LP1"));

        var result = outcome.Should().BeOfType<ForecastOutcome.ZeroForecast>().Subject.Result;
        result.Factors.Source.Should().Be(UsageFactorSource.None);
        result.AnchorDate.Should().Be(new DateOnly(2026, 5, 1));
        result.Days.Should().HaveCount(62).And.OnlyContain(day => day.Kwh == 0m);
    }

    [Fact]
    public void DoesNotForecastWhenTheCurveDoesNotCoverTheHorizon()
    {
        var outcome = Forecast(new DateOnly(2026, 9, 1), 10, ForecastPersonas.NsmpTwentyFourHour("LP1"), ForecastPersonas.Dg1In2026);

        outcome.Should().Be(new ForecastOutcome.NotForecast(NotForecastReason.NoProfileShape));
    }

    [Fact]
    public void DoesNotForecastOnACurveReferenceDataDoesNotHold()
    {
        var outcome = Forecast(new DateOnly(2026, 4, 30), 10, ForecastPersonas.NsmpTwentyFourHour("LP7"), ForecastPersonas.Dg1In2026);

        outcome.Should().Be(new ForecastOutcome.NotForecast(NotForecastReason.NoProfileShape));
    }

    [Fact]
    public void DoesNotForecastAMeterWhoseProfileCannotBeResolved()
    {
        var outcome = Forecast(new DateOnly(2026, 4, 30), 10, new TariffFacts(true, null, null, null));

        outcome.Should().Be(new ForecastOutcome.NotForecast(NotForecastReason.NoTariffValues));
    }
}
