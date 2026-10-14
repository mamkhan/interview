using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// The 24H estimate 4127.5 from 2026-03-01 and the actual 3980.25 from 2026-06-15 are held; today is 2026-10-13. A
// message states the estimate 4127.5 from 2026-05-01, inside the period that began 2026-03-01: it restates what is in
// effect on its date.
public class WhenAStatementRestatesWhatIsInEffect
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<UsageFactorValues> _usageFactor = new();

    private readonly Recorded<UsageFactorValues> _recorded;

    public WhenAStatementRestatesWhatIsInEffect()
    {
        _usageFactor.Record(new(new DateOnly(2026, 3, 1), new UsageFactorValues(4127.5m, null)), Today);
        _usageFactor.Record(new(new DateOnly(2026, 6, 15), new UsageFactorValues(null, 3980.25m)), Today);

        _recorded = _usageFactor.Record(new(new DateOnly(2026, 5, 1), new UsageFactorValues(4127.5m, null)), Today);
    }

    [Fact]
    public void WritesNothing()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Unchanged);
        _recorded.History.Should().BeNull();
        _recorded.Current.Should().BeEmpty();
    }
}
