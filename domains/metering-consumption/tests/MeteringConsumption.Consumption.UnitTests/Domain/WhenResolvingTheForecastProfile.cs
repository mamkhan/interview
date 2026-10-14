using AwesomeAssertions;
using Growth.Contracts;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The meter point's codes and estimated usage factors come from what ESB states is in effect; the smart meter flag
// and the tariff's values from the Profile. Their merge decides the tiers and the curve a forecast uses.
public sealed class WhenResolvingTheForecastProfile
{
    private static ProfileResolution Resolve(MeterPointFacts? configuration, TariffFacts? tariff) =>
        ForecastProfile.Resolve(configuration, tariff);

    private static ForecastProfile Resolved(MeterPointFacts configuration, TariffFacts tariff) =>
        ((ProfileResolution.Resolved)Resolve(configuration, tariff)).Profile;

    private static NotForecastReason Unresolved(MeterPointFacts? configuration, TariffFacts? tariff) =>
        ((ProfileResolution.Unresolved)Resolve(configuration, tariff)).Reason;

    [Fact]
    public void TakesTheCodesAndEstimatesFromTheConfigurationAndTheTariffFromTheProfile()
    {
        var profile = Resolved(ForecastPersonas.TimeOfUse("MCC12", "02", euf24H: 4000m), ForecastPersonas.NsmpDayNight("LP4"));

        profile.Mcc.Should().Be(12);
        profile.OwnLoadProfile.Should().Be("LP2");
        profile.CurveLoadProfile.Should().Be("LP4");
        profile.Layout.Should().Be(CurveLayout.BandedTimeOfUse);
        profile.DuosGroup.Should().Be("DG1");
        profile.Euf24H.Should().Be(4000m);
        profile.IsNsmp.Should().BeTrue();
        profile.OwnUseFirst.Should().BeTrue();
    }

    [Fact]
    public void NamesEachMissingSourceAsItsOwnReason()
    {
        Unresolved(null, ForecastPersonas.NotTimeOfUse).Should().Be(NotForecastReason.NoConfigurationInEffect);
        Unresolved(ForecastPersonas.SingleRate, null).Should().Be(NotForecastReason.NoTariffValues);
        Unresolved(ForecastPersonas.SingleRate, new TariffFacts(false, null, null, null)).Should().Be(NotForecastReason.NoTariffValues);
        Unresolved(ForecastPersonas.SingleRate, new TariffFacts(null, false, null, null)).Should().Be(NotForecastReason.NoTariffValues);
    }

    [Fact]
    public void RefusesAnNsmpTimeOfUseMeterWithNoCalcType()
    {
        Unresolved(ForecastPersonas.TimeOfUse("MCC12", "01"), new TariffFacts(true, true, null, "LP1"))
            .Should().Be(NotForecastReason.NoCalcType);
    }

    [Theory]
    [InlineData("MCC01", true, TariffCalcTypes.TwentyFourHour, CurveLayout.FlatTimeOfUse)]
    [InlineData("MCC02", true, TariffCalcTypes.TwentyFourHour, CurveLayout.FlatTimeOfUse)]
    [InlineData("MCC12", true, TariffCalcTypes.DayNight, CurveLayout.BandedTimeOfUse)]
    [InlineData("MCC01", false, null, CurveLayout.FlatTimeOfUse)]
    [InlineData("MCC12", false, null, CurveLayout.FlatTimeOfUse)]
    [InlineData("MCC16", false, null, CurveLayout.FlatTimeOfUse)]
    [InlineData("MCC02", false, null, CurveLayout.BandedTimeOfUse)]
    public void LaysATimeOfUseMeterOutByItsCalcTypeWhenNsmpAndByItsMccOtherwise(
        string mcc, bool isNsmp, string? calcType, CurveLayout expected)
    {
        Resolved(ForecastPersonas.TimeOfUse(mcc, "01"), new TariffFacts(isNsmp, true, calcType, "LP1"))
            .Layout.Should().Be(expected);
    }

    [Fact]
    public void ShapesOnlyAnNsmpMeterByTheTariffsLoadProfile()
    {
        Resolved(ForecastPersonas.TimeOfUse("MCC12", "01"), new TariffFacts(false, true, null, "LP3"))
            .CurveLoadProfile.Should().Be("LP1");
    }

    [Fact]
    public void LaysAMeterNotOnTimeOfUseOutByItsMcc()
    {
        Resolved(ForecastPersonas.SingleRate, ForecastPersonas.NotTimeOfUse).Layout.Should().Be(CurveLayout.SingleRate);
        Resolved(ForecastPersonas.DayNight, ForecastPersonas.NotTimeOfUse).Layout.Should().Be(CurveLayout.DayNight);
        Unresolved(ForecastPersonas.TimeOfUse("MCC12", "01"), ForecastPersonas.NotTimeOfUse)
            .Should().Be(NotForecastReason.UnsupportedMcc);
    }

    [Fact]
    public void RefusesAnMccTheForecastHasNoRuleFor()
    {
        Unresolved(ForecastPersonas.TimeOfUse("MCC03", "01"), ForecastPersonas.NsmpTwentyFourHour("LP1"))
            .Should().Be(NotForecastReason.UnsupportedMcc);
    }

    [Theory]
    [InlineData("01", "LP1")]
    [InlineData("02", "LP2")]
    [InlineData("10", "LP10")]
    [InlineData("25", "LP25")]
    public void NamesAnEsbLoadProfileCodeAsReferenceDataDoes(string esbCode, string expected)
    {
        ForecastProfile.LoadProfileName(esbCode).Should().Be(expected);
    }

    [Fact]
    public void RefusesAMeterWithNoLoadProfile()
    {
        Unresolved(ForecastPersonas.TimeOfUse("MCC12", ""), ForecastPersonas.NsmpTwentyFourHour("LP1"))
            .Should().Be(NotForecastReason.NoProfileShape);
    }

    [Fact]
    public void ReadsAnMccByItsNumber()
    {
        ForecastProfile.MccNumber("MCC02").Should().Be(2);
        ForecastProfile.MccNumber("NSH").Should().BeNull();
    }
}
