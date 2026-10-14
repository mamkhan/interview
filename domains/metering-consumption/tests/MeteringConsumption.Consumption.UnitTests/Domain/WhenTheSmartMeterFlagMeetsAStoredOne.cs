using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile holds V1's flag that the meter is smart, read at 2026-10-07T04:00Z. A reading at the same moment or
// later that says otherwise replaces it; an earlier one, or one saying the same, changes nothing.
public class WhenTheSmartMeterFlagMeetsAStoredOne
{
    private static readonly DateTimeOffset NextNight = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero);

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenTheSmartMeterFlagMeetsAStoredOne()
    {
        _profile.Apply(new SmartMeterFlagStatementBuilder().Build());
    }

    [Fact]
    public void ReplacesTheFlagWhenALaterReadingDiffers()
    {
        _profile.Apply(new SmartMeterFlagStatementBuilder().NotSmart().ReadAt(NextNight).FromSource("v1-reader:2026-10-08:0").Build());

        _profile.SmartMeterFlag!.IsNsmp.Should().BeFalse();
        _profile.SmartMeterFlag.AsOf.Should().Be(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero));
        _profile.SmartMeterFlag.Source.Should().Be("v1-reader:2026-10-08:0");
    }

    [Fact]
    public void ReplacesTheFlagWhenAReadingAtTheSameMomentDiffers() =>
        _profile.Apply(new SmartMeterFlagStatementBuilder().NotSmart().Build()).Should().BeTrue();

    [Fact]
    public void KeepsTheFlagWhenAnEarlierReadingDiffers()
    {
        _profile.Apply(new SmartMeterFlagStatementBuilder()
            .NotSmart()
            .ReadAt(new DateTimeOffset(2026, 10, 6, 4, 0, 0, TimeSpan.Zero))
            .Build()).Should().BeFalse();

        _profile.SmartMeterFlag!.IsNsmp.Should().BeTrue();
    }

    [Fact]
    public void KeepsTheMomentTheFlagFirstArrivedWhenALaterReadingSaysTheSame()
    {
        _profile.Apply(new SmartMeterFlagStatementBuilder().ReadAt(NextNight).FromSource("v1-reader:2026-10-08:1").Build())
            .Should().BeFalse();

        _profile.SmartMeterFlag!.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
    }
}
