using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 and the actual 3980.25 from 2026-06-15 are held; today is 2026-10-13, so
// today's row is 2026-06-15's, carrying the estimate. A correction for 2026-03-01 states the estimate 4200.0: it
// overwrites that history row, and today's row follows it as no later statement stated an estimate.
public class WhenACorrectionReachesARowThatOnlyCarriedIt
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenACorrectionReachesARowThatOnlyCarriedIt()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, null)), Today);
    }

    [Fact]
    public void OverwritesTheHistoryRowOfItsDate()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Replaced);
        _usageFactor.History.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(4200.0m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)));
    }

    [Fact]
    public void GivesTodaysRowTheCorrectedEstimate() =>
        _usageFactor.Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4200.0m, 3980.25m)));
}
