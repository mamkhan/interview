using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 and 3950.0 from 2026-06-15 are held; today is 2026-10-13, so today's row is
// 2026-06-15's. A message states 3950.0 from 2026-05-01, inside the closed period that ended 2026-06-14 and equal to
// the next period. It differs from what was in effect on 2026-05-01, so it is history of its own; the 2026-06-15 row
// stated its estimate itself and keeps its date and values.
public class WhenAStatementInsideAPeriodEqualsTheNextPeriod
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenAStatementInsideAPeriodEqualsTheNextPeriod()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(3950.0m, null)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 5, 1), new UsageFactorValues(3950.0m, null)), Today);
    }

    [Fact]
    public void KeepsItAsItsOwnHistoryRow() =>
        _usageFactor.History.Select(row => row.EffectiveDate).Should().Equal(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 15));

    [Fact]
    public void MovesNoCurrentRow()
    {
        _recorded.Current.Should().BeEmpty();
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(3950.0m, null)));
    }
}
