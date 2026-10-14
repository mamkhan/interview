using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A timeslot with no rows. An MM300 states the 24H estimate 4127.50000000 from 2026-03-01 and no actual; today is
// 2026-10-13. History keeps what it stated; it is the latest row on or before today, so it is today's current row.
public class WhenTheFirstUsageFactorIsStated
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenTheFirstUsageFactorIsStated()
    {
        _recorded = _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.50000000m, null)), Today);
    }

    [Fact]
    public void KeepsWhatItStatedAsHistory() =>
        _usageFactor.History.Should().Equal(new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)));

    [Fact]
    public void MakesItTodaysCurrentRow() =>
        _usageFactor.Current.Should().Equal(new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)));

    [Fact]
    public void NamesTheHistoryRowAndTheCurrentRowToInsert()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Added);
        _recorded.History.Should().Be(new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)));
        _recorded.Current.Should().Equal(new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Insert,
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null))));
    }
}
