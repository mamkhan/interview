using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile holds a 48-rate tariff on the 24-hour curve with LP1, read at 2026-10-07T04:00Z. A reading at the
// same moment or later with different values replaces it; an earlier one, or one with the same values, changes
// nothing, so the stored AsOf stays the moment the current values first arrived.
public class WhenTariffValuesMeetStoredOnes
{
    private static readonly DateTimeOffset NextNight = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NightBefore = new(2026, 10, 6, 4, 0, 0, TimeSpan.Zero);

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenTariffValuesMeetStoredOnes()
    {
        _profile.Apply(new TariffStatementBuilder().Build());
    }

    [Fact]
    public void ReplacesTheValuesWhenALaterReadingDiffers()
    {
        _profile.Apply(new TariffStatementBuilder()
            .WithCalcType("DayNight")
            .WithEffectiveLoadProfileCode("LP2")
            .ReadAt(NextNight)
            .Build());

        _profile.Tariff!.CalcType.Should().Be("DayNight");
        _profile.Tariff.EffectiveLoadProfileCode.Should().Be("LP2");
        _profile.Tariff.AsOf.Should().Be(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ReplacesTheValuesWhenAReadingAtTheSameMomentDiffers()
    {
        _profile.Apply(new TariffStatementBuilder().WithIsTou(false).Build()).Should().BeTrue();

        _profile.Tariff!.IsTou.Should().BeFalse();
    }

    [Fact]
    public void KeepsTheValuesWhenAnEarlierReadingDiffers()
    {
        _profile.Apply(new TariffStatementBuilder().WithCalcType("DayNight").ReadAt(NightBefore).Build())
            .Should().BeFalse();

        _profile.Tariff!.CalcType.Should().Be("TwentyFourHour");
    }

    [Fact]
    public void ReportsNoChangeWhenALaterReadingStatesTheSameValues() =>
        _profile.Apply(new TariffStatementBuilder().ReadAt(NextNight).Build()).Should().BeFalse();

    [Fact]
    public void KeepsTheMomentTheValuesFirstArrivedWhenALaterReadingStatesTheSameValues()
    {
        _profile.Apply(new TariffStatementBuilder().ReadAt(NextNight).Build());

        _profile.Tariff!.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
    }
}
