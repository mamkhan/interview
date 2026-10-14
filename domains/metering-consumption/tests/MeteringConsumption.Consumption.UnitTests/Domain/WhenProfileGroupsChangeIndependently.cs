using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile holds a meter configuration (MCC01 effective 2026-09-21), a 24H estimated usage factor, the tariff
// values and the smart meter flag. Each group is stated by its own source on its own schedule, so applying one
// leaves the others as they are.
public class WhenProfileGroupsChangeIndependently
{
    private static readonly DateTimeOffset NextNight = new(2026, 10, 8, 4, 0, 0, TimeSpan.Zero);

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenProfileGroupsChangeIndependently()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());
        _profile.Apply([new UsageFactorStatementBuilder().Build()]);
        _profile.Apply(new TariffStatementBuilder().Build());
        _profile.Apply(new SmartMeterFlagStatementBuilder().Build());
    }

    [Fact]
    public void ANewTariffLeavesTheConfigurationTheFactorsAndTheFlag()
    {
        _profile.Apply(new TariffStatementBuilder().WithCalcType("DayNight").ReadAt(NextNight).Build());

        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
        _profile.UsageFactors.Should().HaveCount(1);
        _profile.SmartMeterFlag!.IsNsmp.Should().BeTrue();
    }

    [Fact]
    public void ANewFlagLeavesTheConfigurationTheFactorsAndTheTariff()
    {
        _profile.Apply(new SmartMeterFlagStatementBuilder().NotSmart().ReadAt(NextNight).Build());

        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
        _profile.UsageFactors.Should().HaveCount(1);
        _profile.Tariff!.CalcType.Should().Be("TwentyFourHour");
    }

    [Fact]
    public void ANewConfigurationLeavesTheTariffAndTheFlag()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().WithMeterConfigurationCode("MCC12").EffectiveFrom(new DateOnly(2026, 10, 1)).Build());

        _profile.Tariff!.CalcType.Should().Be("TwentyFourHour");
        _profile.SmartMeterFlag!.IsNsmp.Should().BeTrue();
    }
}
