using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// An MM300 states the load profile, DUoS group and status but no meter configuration code. Over the stored MCC01
// effective 2026-09-21, a later one moves the three codes it carries on and keeps MCC01.
public class WhenAMeterConfigurationCarriesNoCode
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenAMeterConfigurationCarriesNoCode()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());

        _changed = _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode(null)
            .WithLoadProfileCode("02")
            .WithDuosGroup("DG5A")
            .WithMeterPointStatusCode("1")
            .EffectiveFrom(new DateOnly(2026, 10, 1))
            .FromSource("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19", "300")
            .Build());
    }

    [Fact]
    public void KeepsTheStoredMeterConfigurationCode() =>
        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");

    [Fact]
    public void MovesTheCodesItCarriesOn()
    {
        _profile.Configuration!.LoadProfileCode.Should().Be("02");
        _profile.Configuration.DuosGroup.Should().Be("DG5A");
        _profile.Configuration.MeterPointStatusCode.Should().Be("1");
    }

    [Fact]
    public void TakesItsDateAndFile()
    {
        _profile.Configuration!.EffectiveFrom.Should().Be(new DateOnly(2026, 10, 1));
        _profile.Configuration.MessageType.Should().Be("300");
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();

    [Fact]
    public void ReportsNoChangeForARepeatOfItself() =>
        _profile.Apply(new MeterConfigurationStatementBuilder()
                .WithMeterConfigurationCode(null)
                .WithLoadProfileCode("02")
                .WithDuosGroup("DG5A")
                .WithMeterPointStatusCode("1")
                .EffectiveFrom(new DateOnly(2026, 10, 1))
                .FromSource("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19", "300")
                .Build())
            .Should().BeFalse();
}

// The first configuration a supply point receives may be an MM300's or an MM301's: the profile stores the codes it
// has and leaves the rest empty until a message that carries them arrives.
public class WhenTheFirstMeterConfigurationCarriesNoCode
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenTheFirstMeterConfigurationCarriesNoCode()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().WithMeterConfigurationCode(null).Build());
    }

    [Fact]
    public void StoresTheConfigurationWithoutACode()
    {
        _profile.Configuration!.MeterConfigurationCode.Should().BeNull();
        _profile.Configuration.LoadProfileCode.Should().Be("01");
    }
}

// A blank code states no more than a missing one, so it keeps the stored MCC01 rather than overwriting it.
public class WhenAMeterConfigurationCarriesABlankCode
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenAMeterConfigurationCarriesABlankCode()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());

        _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode(" ")
            .WithLoadProfileCode("02")
            .EffectiveFrom(new DateOnly(2026, 10, 1))
            .Build());
    }

    [Fact]
    public void KeepsTheStoredMeterConfigurationCode() =>
        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");

    [Fact]
    public void MovesTheCodesItCarriesOn() =>
        _profile.Configuration!.LoadProfileCode.Should().Be("02");
}

// An MM301 states the meter configuration code and DUoS group but no load profile or status. Over the stored MCC01,
// 01, DG1 and E effective 2026-09-21, a later one moves the two codes it carries on and keeps the other two.
public class WhenAMeterConfigurationCarriesOnlySomeCodes
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenAMeterConfigurationCarriesOnlySomeCodes()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());

        _changed = _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithMeterConfigurationCode("MCC12")
            .WithLoadProfileCode(null)
            .WithDuosGroup("DG2")
            .WithMeterPointStatusCode(null)
            .EffectiveFrom(new DateOnly(2026, 10, 1))
            .FromSource("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19", "301")
            .Build());
    }

    [Fact]
    public void MovesTheCodesItCarriesOn()
    {
        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC12");
        _profile.Configuration.DuosGroup.Should().Be("DG2");
    }

    [Fact]
    public void KeepsTheStoredCodesItDoesNotCarry()
    {
        _profile.Configuration!.LoadProfileCode.Should().Be("01");
        _profile.Configuration.MeterPointStatusCode.Should().Be("E");
    }

    [Fact]
    public void TakesItsDateAndFile()
    {
        _profile.Configuration!.EffectiveFrom.Should().Be(new DateOnly(2026, 10, 1));
        _profile.Configuration.MessageType.Should().Be("301");
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();
}

// A blank load profile, DUoS group or status states no more than a missing one, so each keeps the stored value.
public class WhenAMeterConfigurationCarriesBlankCodes
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenAMeterConfigurationCarriesBlankCodes()
    {
        _profile.Apply(new MeterConfigurationStatementBuilder().Build());

        _profile.Apply(new MeterConfigurationStatementBuilder()
            .WithLoadProfileCode(" ")
            .WithDuosGroup("")
            .WithMeterPointStatusCode("")
            .EffectiveFrom(new DateOnly(2026, 10, 1))
            .Build());
    }

    [Fact]
    public void KeepsEveryStoredCode()
    {
        _profile.Configuration!.LoadProfileCode.Should().Be("01");
        _profile.Configuration.DuosGroup.Should().Be("DG1");
        _profile.Configuration.MeterPointStatusCode.Should().Be("E");
    }
}
