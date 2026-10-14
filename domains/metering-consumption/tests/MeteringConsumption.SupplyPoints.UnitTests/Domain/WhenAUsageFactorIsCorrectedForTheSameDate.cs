using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A message stated the 24H estimate 4127.5 and actual 3842.5173 from 2026-03-01; today is 2026-10-13. A correction for
// 2026-03-01 states only the estimate 4200.0. One history row per date: the correction's estimate overwrites that row's
// and the actual it leaves out stays, and today's current row takes the same.
public class WhenAUsageFactorIsCorrectedForTheSameDate
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenAUsageFactorIsCorrectedForTheSameDate()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, 3842.5173m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, null)), Today);
    }

    [Fact]
    public void OverwritesTheStatedValueOnThatDatesHistoryRowAndKeepsTheOther() =>
        _usageFactor.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, 3842.5173m)));

    [Fact]
    public void UpdatesTodaysCurrentRow() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, 3842.5173m)));

    [Fact]
    public void NamesTheHistoryRowToOverwriteAndTheCurrentRowToUpdate()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Replaced);
        _recorded.History.Should().Be(new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, 3842.5173m)));
        _recorded.Current.Should().Equal(new CurrentWrite<UsageFactorValues>(CurrentWriteKind.Update,
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, 3842.5173m))));
    }
}
