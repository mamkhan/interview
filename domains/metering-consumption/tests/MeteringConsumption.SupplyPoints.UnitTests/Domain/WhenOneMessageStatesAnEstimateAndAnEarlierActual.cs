using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The real MM332 for MPRN 10900000332 lists the 24H estimate 5033.03824582 from 2026-09-25 first and the 24H actual
// 4276.94863062 from 2026-07-30 second; today is 2026-10-13. Recorded in that order or the other, history keeps each
// statement's own value and today's row, 2026-09-25's, holds both.
public class WhenOneMessageStatesAnEstimateAndAnEarlierActual
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private static readonly Dated<UsageFactorValues> Estimate = new(new DateOnly(2026, 9, 25), new UsageFactorValues(5033.03824582m, null));
    private static readonly Dated<UsageFactorValues> Actual = new(new DateOnly(2026, 7, 30), new UsageFactorValues(null, 4276.94863062m));

    private readonly KeyRows<UsageFactorValues> _asListed = new();
    private readonly KeyRows<UsageFactorValues> _reversed = new();

    public WhenOneMessageStatesAnEstimateAndAnEarlierActual()
    {
        _asListed.Record(Estimate, Today);
        _asListed.Record(Actual, Today);

        _reversed.Record(Actual, Today);
        _reversed.Record(Estimate, Today);
    }

    [Fact]
    public void KeepsEachStatementsOwnValueAsHistory()
    {
        _asListed.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 7, 30), new UsageFactorValues(null, 4276.94863062m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 25), new UsageFactorValues(5033.03824582m, null)));
        _reversed.History.Should().Equal(_asListed.History);
    }

    [Fact]
    public void GivesTodaysRowTheActualStatedFromBeforeIt()
    {
        _asListed.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 25), new UsageFactorValues(5033.03824582m, 4276.94863062m)));
        _reversed.Current.Should().Equal(_asListed.Current);
    }
}
