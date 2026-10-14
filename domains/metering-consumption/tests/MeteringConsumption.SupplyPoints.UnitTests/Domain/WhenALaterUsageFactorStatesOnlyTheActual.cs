using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// An MM300 stated the 24H estimate 4127.5 from 2026-03-01. An MM332 states only the 24H actual 3980.25 from 2026-06-15,
// the closing read of an exchange; today is 2026-10-13. History keeps each statement's own value; the row from
// 2026-06-15 is now today's, carrying the estimate, and the 2026-03-01 row before it goes from current.
public class WhenALaterUsageFactorStatesOnlyTheActual
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenALaterUsageFactorStatesOnlyTheActual()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);
    }

    [Fact]
    public void KeepsOnlyWhatEachStatementStatedAsHistory() =>
        _usageFactor.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)));

    [Fact]
    public void CarriesTheEstimateIntoTodaysRowAndDropsTheRowBeforeIt() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4127.5m, 3980.25m)));

    [Fact]
    public void NamesTheNewRowsAndTheRowToDelete()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Added);
        _recorded.History.Should().Be(new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)));
        _recorded.Current.Should().BeEquivalentTo(
        [
            new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Insert,
                new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4127.5m, 3980.25m))),
            new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Delete,
                new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null))),
        ]);
    }
}
