using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A supply point with no meters; today is 2026-10-13. A message lists meter 90003311 from 2026-09-16 with no category,
// location or exchanged reference, which ESB may leave out. Nothing was known of the meter before, so the statement is
// still kept: the meter is in place from that date.
public class WhenAMeterIsFirstStatedWithNoDetails
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterDetails> _meter = new();

    private readonly Recorded<MeterDetails> _recorded;

    public WhenAMeterIsFirstStatedWithNoDetails()
    {
        _recorded = _meter.Record(new MeterStatementBuilder().WithDetails(null, null, null).Build(), Today);
    }

    [Fact]
    public void KeepsTheMeterFromItsDate()
    {
        var stated = new Dated<MeterDetails>(new DateOnly(2026, 9, 16), new MeterDetails(null, null, null));

        _recorded.Kind.Should().Be(RecordOutcomeKind.Added);
        _meter.History.Should().Equal(stated);
        _meter.Current.Should().Equal(stated);
    }
}
