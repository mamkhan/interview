using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Each usage factor states an estimate or an actual, never both, for one of the six timeslots ESB defines for
// usage factors. Anything else is a publisher defect or a code this table has no column for, so it is refused,
// which dead-letters the message for someone to look at.
public class WhenAUsageFactorBreaksItsContract
{
    private const string Source = "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25";

    private static readonly DateOnly EffectiveFrom = new(2026, 9, 21);

    [Fact]
    public void RefusesAFactorWithBothValues()
    {
        var act = () => UsageFactorStatement.Of("24H", EffectiveFrom, estimated: 3910.0m, actual: 3842.5173m, Source);

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*both*");
    }

    [Fact]
    public void RefusesAFactorWithNeitherValue()
    {
        var act = () => UsageFactorStatement.Of("24H", EffectiveFrom, estimated: null, actual: null, Source);

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*neither*");
    }

    [Theory]
    [InlineData("01X")]
    [InlineData("")]
    [InlineData("24h")]
    public void RefusesATimeslotOutsideTheSix(string timeslot)
    {
        var act = () => UsageFactorStatement.Of(timeslot, EffectiveFrom, estimated: 3910.0m, actual: null, Source);

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*timeslot*");
    }

    [Fact]
    public void RefusesAFactorWithNoSource()
    {
        var act = () => UsageFactorStatement.Of("24H", EffectiveFrom, estimated: 3910.0m, actual: null, source: "");

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*source*");
    }
}
