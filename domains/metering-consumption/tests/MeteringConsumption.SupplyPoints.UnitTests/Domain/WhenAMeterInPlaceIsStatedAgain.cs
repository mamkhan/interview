using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Meter 90003311 is in place from 2026-09-16 (the real MM331 sample); today is 2026-10-13. A change of supplier MM320 on
// 2026-10-01 states it again, as ESB does every time a meter is still there, with category RM106 and location 07 and no
// exchanged meter reference. Nothing about the meter changed.
public class WhenAMeterInPlaceIsStatedAgain
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterDetails> _meter = new();

    private readonly Recorded<MeterDetails> _recorded;

    public WhenAMeterInPlaceIsStatedAgain()
    {
        _meter.Record(new MeterStatementBuilder().Build(), Today);
        _recorded = _meter.Record(new MeterStatementBuilder().StartingOn(new DateOnly(2026, 10, 1)).WithDetails("RM106", "07", null).Build(), Today);
    }

    [Fact]
    public void WritesNothing()
    {
        _recorded.Kind.Should().Be(RecordOutcomeKind.Unchanged);
        _meter.History.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 9, 16));
        _meter.Current.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 9, 16));
    }
}
