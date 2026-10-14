using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// One message states the same timeslot, kind and effective date twice. The same value twice is one factor;
// two different values cannot both be the latest version, so the message is refused and none of it is kept.
public class WhenOneMessageStatesAFactorTwice
{
    private static readonly UsageFactorSlot Estimated24H = new("24H", UsageFactorKind.Estimated);
    private static readonly UsageFactorSlot Estimated00N = new("00N", UsageFactorKind.Estimated);

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    [Fact]
    public void CountsTheSameValueOnce()
    {
        _profile.Apply([new UsageFactorStatementBuilder().Build(), new UsageFactorStatementBuilder().Build()]);

        _profile.UsageFactors.Should().ContainSingle().Which.Value.Value.Should().Be(3910.0m);
    }

    [Fact]
    public void RefusesTwoDifferentValuesAndKeepsNoneOfTheMessage()
    {
        var act = () => _profile.Apply(
        [
            new UsageFactorStatementBuilder().ForTimeslot("00N").Estimated(1250.5m).Build(),
            new UsageFactorStatementBuilder().Estimated(3910.0m).Build(),
            new UsageFactorStatementBuilder().Estimated(3911.0m).Build(),
        ]);

        act.Should().Throw<ProfileStatementRejectedException>().WithMessage("*24H*");
        _profile.UsageFactors.Should().NotContainKey(Estimated00N).And.NotContainKey(Estimated24H);
    }
}
