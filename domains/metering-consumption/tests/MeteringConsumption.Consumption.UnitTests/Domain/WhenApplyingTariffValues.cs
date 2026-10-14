using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A supply point's first tariff values: a 48-rate tariff on the 24-hour curve with load profile LP1, read by the
// V1 reader at 2026-10-07T04:00Z.
public class WhenApplyingTariffValues
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenApplyingTariffValues()
    {
        _changed = _profile.Apply(new TariffStatementBuilder().Build());
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();

    [Fact]
    public void StoresTheTariffValues()
    {
        _profile.Tariff!.IsTou.Should().BeTrue();
        _profile.Tariff.CalcType.Should().Be("TwentyFourHour");
        _profile.Tariff.EffectiveLoadProfileCode.Should().Be("LP1");
    }

    [Fact]
    public void StoresWhenTheValuesWereReadAndWhoReadThem()
    {
        _profile.Tariff!.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        _profile.Tariff.Source.Should().Be("v1-reader/tariff-values");
    }
}
