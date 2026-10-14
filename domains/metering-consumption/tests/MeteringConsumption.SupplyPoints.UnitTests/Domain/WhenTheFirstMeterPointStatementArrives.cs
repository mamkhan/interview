using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A supply point with no meter point rows. ESB states the meter point from 2026-03-01 with MCC01, load profile 01,
// DUoS group DG1, status E and networks reference NRN-hash-mm105 (row A of the Diagrams 4 example, an MM105); today is
// 2026-10-13. The reference is one of the meter point's values, kept with its codes.
public class WhenTheFirstMeterPointStatementArrives
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterPointValues> _meterPoint = new();

    public WhenTheFirstMeterPointStatementArrives()
    {
        _meterPoint.Record(new MeterPointStatementBuilder().Build(), Today);
    }

    [Fact]
    public void KeepsTheCodesAndTheReferenceAsHistoryAndAsTodaysRow()
    {
        var stated = new Dated<MeterPointValues>(new DateOnly(2026, 3, 1), new MeterPointValues("MCC01", "01", "DG1", "E", "NRN-hash-mm105"));

        _meterPoint.History.Should().Equal(stated);
        _meterPoint.Current.Should().Equal(stated);
    }
}
