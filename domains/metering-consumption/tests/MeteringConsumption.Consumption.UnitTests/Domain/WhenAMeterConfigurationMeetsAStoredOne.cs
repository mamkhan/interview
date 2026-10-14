using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile already holds MCC01 effective 2026-09-21. The latest version of the configuration is the one
// with the latest effective date, whatever order the files arrive in; on the same date, the one applied
// last wins, so a re-sent correction replaces the original.
public class WhenAMeterConfigurationMeetsAStoredOne
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenAMeterConfigurationMeetsAStoredOne()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());
    }

    [Fact]
    public void KeepsTheStoredCodesWhenTheNewOneTakesEffectEarlier()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode("MCC02")
            .EffectiveFrom(new DateOnly(2026, 9, 1))
            .Build());

        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
    }

    [Fact]
    public void ReportsNoChangeWhenTheNewOneTakesEffectEarlier() =>
        _profile.Apply(new MeterConfigurationStatementBuilder().EffectiveFrom(new DateOnly(2026, 9, 1)).Build())
            .Should().BeFalse();

    [Fact]
    public void ReportsNoChangeForARepeatOfTheStoredStatement() =>
        _profile.Apply(new MeterConfigurationStatementBuilder().Build()).Should().BeFalse();

    [Fact]
    public void ReplacesTheStoredCodesWhenTheNewOneTakesEffectLater()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode("MCC02")
            .EffectiveFrom(new DateOnly(2026, 10, 1))
            .Build());

        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC02");
    }

    [Fact]
    public void ReplacesTheStoredCodesWithACorrectionForTheSameDate()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithDuosGroup("DG5A")
            .FromSource("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19", "332")
            .Build());

        _profile.Configuration!.DuosGroup.Should().Be("DG5A");
        _profile.Configuration.Source.Should().Be("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19");
    }
}
