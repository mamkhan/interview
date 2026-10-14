using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 and the actual 3980.25 from 2026-06-15 are held; today is 2026-10-13, so
// today's row is 2026-06-15's, which carried the estimate from 2026-03-01. A backdated message states the estimate
// 4300.0 from 2026-05-01. It is before today's row, so it gets no current row of its own; today's row follows it,
// because no statement after 2026-05-01 stated an estimate of its own.
public class WhenABackdatedValueReachesARowThatOnlyCarriedIt
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenABackdatedValueReachesARowThatOnlyCarriedIt()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 5, 1), new UsageFactorValues(4300.0m, null)), Today);
    }

    [Fact]
    public void KeepsItAsItsOwnHistoryRow() =>
        _usageFactor.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 5, 1), new UsageFactorValues(4300.0m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)));

    [Fact]
    public void GivesTodaysRowTheBackdatedEstimate() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4300.0m, 3980.25m)));

    [Fact]
    public void UpdatesOnlyTodaysRow() =>
        _recorded.Current.Should().Equal(new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Update,
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4300.0m, 3980.25m))));
}
