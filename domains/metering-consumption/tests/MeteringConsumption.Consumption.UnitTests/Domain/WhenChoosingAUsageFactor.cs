using System.Globalization;
using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Ten days of 12 kWh up to 2026-04-30 give an own-use window; three do not. Estimates of 4,800 kWh fan out to exactly
// 100 kWh on each of 48 tiers. The acceptable estimate range is 1,050 to 16,800 kWh, both ends excluded.
public sealed class WhenChoosingAUsageFactor
{
    private static readonly DateOnly LatestReadDate = new(2026, 4, 30);

    private static UsageFactors? Choose(
        MeterPointFacts configuration, TariffFacts tariff, int readDays, AnnualAverageUsage? annualAverage = null) =>
        Choose(configuration, tariff, new ConsumptionWindowBuilder().DailyUse(LatestReadDate, readDays, 6m, 6m), annualAverage);

    private static UsageFactors? Choose(
        MeterPointFacts configuration, TariffFacts tariff, ConsumptionWindowBuilder days, AnnualAverageUsage? annualAverage = null)
    {
        var profile = ((ProfileResolution.Resolved)ForecastProfile.Resolve(configuration, tariff)).Profile;
        return UsageFactorSelection.Select(
            profile, days.Build(), V1LoadProfiles.Curve(profile.OwnLoadProfile), annualAverage, ForecastPersonas.Settings);
    }

    [Fact]
    public void TriesOwnUseFirstForAnMcc12Meter()
    {
        Choose(ForecastPersonas.TimeOfUse("MCC12", "01", euf24H: 4800m), ForecastPersonas.NsmpTwentyFourHour("LP1"), 10,
                new AnnualAverageUsage(4800m, null, null))!
            .Source.Should().Be(UsageFactorSource.Puf);
    }

    [Fact]
    public void TriesTheAreaAverageBeforeTheEstimateForAnMcc12MeterWithoutOwnUse()
    {
        Choose(ForecastPersonas.TimeOfUse("MCC12", "01", euf24H: 4800m), ForecastPersonas.NsmpTwentyFourHour("LP1"), 3,
                new AnnualAverageUsage(4800m, null, null))!
            .Source.Should().Be(UsageFactorSource.Aau);
    }

    [Fact]
    public void TriesTheEstimateFirstForAnMcc1Meter()
    {
        Choose(ForecastPersonas.TimeOfUse("MCC01", "01", euf24H: 4800m), ForecastPersonas.NsmpTwentyFourHour("LP1"), 10)!
            .Source.Should().Be(UsageFactorSource.Euf);
        Choose(ForecastPersonas.SingleRate with { Euf24H = 4800m }, ForecastPersonas.NotTimeOfUse, 10)!
            .Source.Should().Be(UsageFactorSource.Euf);
    }

    [Fact]
    public void FansATimeOfUseEstimateOrAverageEvenlyOverTheFortyEightTiers()
    {
        var estimate = Choose(ForecastPersonas.TimeOfUse("MCC01", "01", euf24H: 4800m), ForecastPersonas.NsmpTwentyFourHour("LP1"), 3)!;
        var average = Choose(ForecastPersonas.TimeOfUse("MCC12", "01"), ForecastPersonas.NsmpTwentyFourHour("LP1"), 3,
            new AnnualAverageUsage(4800m, null, null))!;

        estimate.PerTier.Should().HaveCount(48).And.AllBeEquivalentTo(100m);
        average.PerTier.Should().HaveCount(48).And.AllBeEquivalentTo(100m);
    }

    [Theory]
    [InlineData("1050", UsageFactorSource.Puf)]
    [InlineData("1050.01", UsageFactorSource.Euf)]
    [InlineData("16799.99", UsageFactorSource.Euf)]
    [InlineData("16800", UsageFactorSource.Puf)]
    public void AcceptsAnEstimateOnlyStrictlyInsideTheRange(string euf24H, UsageFactorSource expected)
    {
        Choose(ForecastPersonas.SingleRate with { Euf24H = decimal.Parse(euf24H, CultureInfo.InvariantCulture) },
                ForecastPersonas.NotTimeOfUse, 10)!
            .Source.Should().Be(expected);
    }

    [Fact]
    public void TakesADayNightMetersEstimateOnlyAsANightAndDayPair()
    {
        Choose(ForecastPersonas.DayNight with { Euf00N = 2000m, Euf00D = 3000m }, ForecastPersonas.NotTimeOfUse, 10)!
            .PerTier.Should().Equal(2000m, 3000m);
        Choose(ForecastPersonas.DayNight with { Euf24H = 4800m }, ForecastPersonas.NotTimeOfUse, 10)!
            .Source.Should().Be(UsageFactorSource.Puf);
    }

    [Fact]
    public void KeepsATimeOfUseTierThatUsedNothing()
    {
        var factors = Choose(ForecastPersonas.TimeOfUse("MCC12", "01"), ForecastPersonas.NsmpTwentyFourHour("LP1"), 10)!;

        factors.Source.Should().Be(UsageFactorSource.Puf);
        factors.PerTier[5].Should().Be(0m);
    }

    [Fact]
    public void FindsNoFactorWithoutOwnUseEstimateOrAreaAverage()
    {
        Choose(ForecastPersonas.TimeOfUse("MCC12", "01"), ForecastPersonas.NsmpTwentyFourHour("LP1"), 3).Should().BeNull();
    }
}
