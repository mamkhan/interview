using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A supply point with no meters; today is 2026-10-13. The real MM331 sample puts meter 90003311 in place on 2026-09-16:
// category RM106, location 07, exchanged meter reference 000000000090003311.
public class WhenAMeterIsFirstPutInPlace
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterDetails> _meter = new();

    public WhenAMeterIsFirstPutInPlace()
    {
        _meter.Record(new MeterStatementBuilder().Build(), Today);
    }

    [Fact]
    public void KeepsTheStatedDetailsAsHistoryAndAsTodaysRow()
    {
        var stated = new Dated<MeterDetails>(new DateOnly(2026, 9, 16), new MeterDetails("RM106", "07", "000000000090003311"));

        _meter.History.Should().Equal(stated);
        _meter.Current.Should().Equal(stated);
    }
}
