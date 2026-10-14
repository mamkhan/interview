using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// On 2026-10-13 the 24H estimate 4127.5 from 2026-03-01 (today's row then) and 3950.0 from 2026-11-01 (a future row)
// were recorded. It is now 2026-11-05: the 2026-11-01 row is today's, and the 2026-03-01 row before it stays in current
// until a statement changes something.
public class WhenAFutureRowHasBecomeTodays
{
    private static readonly DateOnly Then = new(2026, 10, 13);
    private static readonly DateOnly Today = new(2026, 11, 5);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    public WhenAFutureRowHasBecomeTodays()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Then);
        _usageFactor.Record(new(new DateOnly(2026, 11, 1), new UsageFactorValues(3950.0m, null)), Then);
    }

    // The actual 4000.0 from 2026-12-01 is a change, so the row before today's goes.
    [Fact]
    public void DeletesTheRowBeforeTodaysWhenAStatementChangesSomething()
    {
        var recorded = _usageFactor.Record(new(new DateOnly(2026, 12, 1), new UsageFactorValues(null, 4000.0m)), Today);

        recorded.Current.Should().BeEquivalentTo(
        [
            new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Insert,
                new Dated<UsageFactorValues>(new DateOnly(2026, 12, 1), new UsageFactorValues(3950.0m, 4000.0m))),
            new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Delete,
                new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null))),
        ]);
        _usageFactor.Current.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));
    }

    // The estimate 3950.0 from 2026-11-01 again changes nothing, so nothing is written, the old row included.
    [Fact]
    public void WritesNothingWhenAStatementChangesNothing()
    {
        var recorded = _usageFactor.Record(new(new DateOnly(2026, 11, 1), new UsageFactorValues(3950.0m, null)), Today);

        recorded.Current.Should().BeEmpty();
        _usageFactor.Current.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 3, 1), new DateOnly(2026, 11, 1));
    }
}
