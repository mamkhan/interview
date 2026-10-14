using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 and the actual 3980.25 from 2026-06-15 are held; today is 2026-10-13, so
// current holds only the 2026-06-15 row. ESB resends the first message in another file: the estimate in effect on
// 2026-03-01, read from history as current no longer reaches back that far, is already 4127.5.
public class WhenTheSameUsageFactorStatementArrivesAgain
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenTheSameUsageFactorStatementArrivesAgain()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
    }

    [Fact]
    public void WritesNothing()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Unchanged);
        _recorded.History.Should().BeNull();
        _recorded.Current.Should().BeEmpty();
    }

    [Fact]
    public void KeepsTheRowsAsTheyWere()
    {
        _usageFactor.History.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 15));
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4127.5m, 3980.25m)));
    }
}
