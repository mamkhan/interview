using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 is held and is today's row on 2026-10-13. A message states only the actual
// 3980.25 from 2026-11-01. Current keeps today's row and holds what is already known about 2026-11-01, carrying the
// estimate into it.
public class WhenAUsageFactorIsStatedForAFutureDate
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenAUsageFactorIsStatedForAFutureDate()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 11, 1), new UsageFactorValues(null, 3980.25m)), Today);
    }

    [Fact]
    public void KeepsTodaysRowAndAddsTheFutureOne() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 11, 1), new UsageFactorValues(4127.5m, 3980.25m)));

    [Fact]
    public void InsertsOnlyTheFutureRow() =>
        _recorded.Current.Should().Equal(new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Insert,
            new Dated<UsageFactorValues>(new DateOnly(2026, 11, 1), new UsageFactorValues(4127.5m, 3980.25m))));
}
