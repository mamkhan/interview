using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A supply point's first smart meter flag: V1 treats the meter as smart, read by the V1 reader at
// 2026-10-07T04:00Z.
public class WhenApplyingTheSmartMeterFlag
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenApplyingTheSmartMeterFlag()
    {
        _changed = _profile.Apply(new SmartMeterFlagStatementBuilder().Build());
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();

    [Fact]
    public void StoresTheFlagWithWhenItWasReadAndWhereItCameFrom()
    {
        _profile.SmartMeterFlag!.IsNsmp.Should().BeTrue();
        _profile.SmartMeterFlag.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        _profile.SmartMeterFlag.Source.Should().Be("v1-reader:2026-10-07:1");
    }

    [Fact]
    public void RefusesAFlagWithNoSource()
    {
        var act = () => new SmartMeterFlagStatementBuilder().FromSource(" ").Build();

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*source*");
    }
}
