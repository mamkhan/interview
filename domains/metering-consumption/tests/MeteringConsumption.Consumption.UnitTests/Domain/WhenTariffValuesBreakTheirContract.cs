using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// A tariff names its curve as one of the two calc types Growth's contract defines, or none, and says who resolved
// it. Anything else is a publisher defect, so it is refused, which dead-letters the message for someone to look at.
public class WhenTariffValuesBreakTheirContract
{
    [Theory]
    [InlineData("3")]
    [InlineData("DayPeakNight")]
    [InlineData("twentyfourhour")]
    [InlineData("")]
    public void RefusesACalcTypeOutsideTheTwo(string calcType)
    {
        var act = () => new TariffStatementBuilder().WithCalcType(calcType).Build();

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*calc type*");
    }

    [Fact]
    public void RefusesTariffValuesWithNoSource()
    {
        var act = () => new TariffStatementBuilder().FromSource("").Build();

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*source*");
    }
}
