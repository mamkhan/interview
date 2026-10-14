using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Every statement names the sealed file it came from, so one without a source is a publisher defect: the
// statement is refused, which dead-letters the message, rather than stored untraceable. The four codes may be
// empty; a message that does not carry one leaves the stored code in place.
public class WhenAMeterConfigurationIsIncomplete
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    [Fact]
    public void RefusesAStatementWithoutASourceAndStoresNothing()
    {
        var act = () => _profile.Apply(new MeterConfigurationStatementBuilder().FromSource("", "332").Build());

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*source*");
        _profile.Configuration.Should().BeNull();
    }
}
