using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Held for 24H, today 2026-10-13: the estimate 4127.5 from 2026-03-01, the actual 3980.25 from 2026-06-15, the
// estimate 3950.0 from 2026-11-01 and the actual 4000.0 from 2026-12-01. Current holds today's 2026-06-15 row
// (4127.5, 3980.25) and the future rows 2026-11-01 (3950.0, 3980.25) and 2026-12-01 (3950.0, 4000.0). A backdated
// message states the estimate 4300.0 from 2026-05-01: today's row only carried its estimate and takes 4300.0; the
// 2026-11-01 statement stated an estimate of its own, so that row and every row after it keep 3950.0.
public class WhenABackdatedValueMeetsALaterStatementOfItsOwn
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenABackdatedValueMeetsALaterStatementOfItsOwn()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 11, 1), new UsageFactorValues(3950.0m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 12, 1), new UsageFactorValues(null, 4000.0m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 5, 1), new UsageFactorValues(4300.0m, null)), Today);
    }

    [Fact]
    public void ReachesOnlyTheRowsBeforeThatStatement() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4300.0m, 3980.25m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 11, 1), new UsageFactorValues(3950.0m, 3980.25m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 12, 1), new UsageFactorValues(3950.0m, 4000.0m)));

    [Fact]
    public void UpdatesOnlyTheRowThatChanged() =>
        _recorded.Current.Should().Equal(new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Update,
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4300.0m, 3980.25m))));
}
