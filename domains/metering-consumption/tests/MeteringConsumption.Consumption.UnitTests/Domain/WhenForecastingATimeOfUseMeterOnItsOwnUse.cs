using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Three real production NSMP time-of-use meters (V1TimeOfUseMeters), forecast from 2026-07-28 on their own use. The own
// use is divided by the curve of the meter's own ESB profile, as V1 does: the LP25 smart blend for the two smart
// meters, LP2's night and day bands for the third. Expected day-1 and 62-day values were derived with the proof of
// concept's calculation under Native rounding with V1's denominator, the rule set this module keeps.
public sealed class WhenForecastingATimeOfUseMeterOnItsOwnUse
{
    private static ForecastResult Forecast(
        MeterPointFacts configuration, TariffFacts tariff, DateOnly baseline, decimal[] baselineRegisters, decimal[] latestRegisters)
    {
        var window = new ConsumptionWindowBuilder()
            .Day(baseline, baselineRegisters)
            .Day(V1TimeOfUseMeters.LatestReadDate, [.. latestRegisters.Zip(baselineRegisters, (latest, first) => latest - first)])
            .Build();

        var outcome = ForecastCalculation.Calculate(
            window, configuration, tariff, V1LoadProfiles.Curve, ForecastPersonas.Dg1In2026, ForecastPersonas.Settings);
        return ((ForecastOutcome.Computed)outcome).Result;
    }

    [Fact]
    public void ForecastsASmartMeterOnATwentyFourHourTariff()
    {
        var result = Forecast(
            ForecastPersonas.TimeOfUse("MCC16", "25"),
            ForecastPersonas.NsmpTwentyFourHour("LP1"),
            V1TimeOfUseMeters.SmartFlatBaseline,
            V1TimeOfUseMeters.SmartFlatBaselineRegisters,
            V1TimeOfUseMeters.SmartFlatLatestRegisters);

        result.Factors.Source.Should().Be(UsageFactorSource.Puf);
        result.Profile.Layout.Should().Be(CurveLayout.FlatTimeOfUse);
        result.Days[0].Kwh.Should().Be(2.4897646889262805710337005802m);
        result.TotalKwh.Should().Be(160.95546584628022799228898945m);
    }

    [Fact]
    public void ForecastsASmartMeterOnADayNightTariff()
    {
        var result = Forecast(
            ForecastPersonas.TimeOfUse("MCC16", "25"),
            ForecastPersonas.NsmpDayNight("LP2"),
            V1TimeOfUseMeters.SmartBandedBaseline,
            V1TimeOfUseMeters.SmartBandedBaselineRegisters,
            V1TimeOfUseMeters.SmartBandedLatestRegisters);

        result.Profile.Layout.Should().Be(CurveLayout.BandedTimeOfUse);
        result.Days[0].Kwh.Should().Be(9.241385542573061276343537083m);
        result.TotalKwh.Should().Be(597.91597329514171077371412182m);
    }

    [Fact]
    public void ForecastsADayNightMeterOnItsOwnBands()
    {
        var result = Forecast(
            ForecastPersonas.TimeOfUse("MCC12", "02"),
            ForecastPersonas.NsmpDayNight("LP2"),
            V1TimeOfUseMeters.DayNightOnlyBaseline,
            V1TimeOfUseMeters.DayNightOnlyBaselineRegisters,
            V1TimeOfUseMeters.DayNightOnlyLatestRegisters);

        result.Days[0].Kwh.Should().Be(2.4865472078297075270367590318m);
        result.TotalKwh.Should().Be(160.87915987382891500443480044m);
    }

    [Fact]
    public void DividesASmartMetersOwnUseByItsOwnProfileNotItsTariffs()
    {
        var result = Forecast(
            ForecastPersonas.TimeOfUse("MCC16", "25"),
            ForecastPersonas.NsmpTwentyFourHour("LP1"),
            V1TimeOfUseMeters.SmartFlatBaseline,
            V1TimeOfUseMeters.SmartFlatBaselineRegisters,
            V1TimeOfUseMeters.SmartFlatLatestRegisters);

        result.Profile.OwnLoadProfile.Should().Be("LP25");
        result.Profile.CurveLoadProfile.Should().Be("LP1");
        result.Factors.Denominators.Should().AllBeEquivalentTo(0.0581037721m);
    }
}
