using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 is held; today is 2026-10-13, so it is today's row. A message arriving
// afterwards states the estimate 4000.0 and the actual 3800.0 from 2026-01-01. It is before today's row, so it gets no
// current row; today's row keeps the estimate its own statement gave and takes the actual it had none of.
public class WhenAUsageFactorStatementPrecedesEveryRow
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    public WhenAUsageFactorStatementPrecedesEveryRow()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);

        _usageFactor.Record(new(new DateOnly(2026, 1, 1), new UsageFactorValues(4000.0m, 3800.0m)), Today);
    }

    [Fact]
    public void KeepsItAsTheFirstHistoryRow() =>
        _usageFactor.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 1, 1), new UsageFactorValues(4000.0m, 3800.0m)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)));

    [Fact]
    public void GivesTodaysRowOnlyTheValueItHadNoStatementOf() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, 3800.0m)));
}
