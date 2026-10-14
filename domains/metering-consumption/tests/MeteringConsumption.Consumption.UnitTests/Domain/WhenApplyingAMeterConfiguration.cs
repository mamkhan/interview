using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A supply point with no profile yet receives its first meter configuration: the builder's default, MCC01,
// load profile 01, DUoS group DG1, energised, effective 2026-09-21.
public class WhenApplyingAMeterConfiguration
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenApplyingAMeterConfiguration()
    {
        _changed = _profile.Apply(new MeterConfigurationStatementBuilder().Build());
    }

    [Fact]
    public void StoresTheFourCodesAsSent()
    {
        _profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
        _profile.Configuration.LoadProfileCode.Should().Be("01");
        _profile.Configuration.DuosGroup.Should().Be("DG1");
        _profile.Configuration.MeterPointStatusCode.Should().Be("E");
    }

    [Fact]
    public void StoresTheEffectiveDateAndTheFileItCameFrom()
    {
        _profile.Configuration!.EffectiveFrom.Should().Be(new DateOnly(2026, 9, 21));
        _profile.Configuration.Source.Should().Be("801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25");
        _profile.Configuration.MessageType.Should().Be("332");
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();

    [Fact]
    public void LeavesTheUsageFactorsAsTheyWere() => _profile.UsageFactors.Should().BeEmpty();
}
