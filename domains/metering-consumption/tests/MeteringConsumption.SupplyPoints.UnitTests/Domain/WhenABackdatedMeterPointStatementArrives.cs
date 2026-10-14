using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Diagrams 4: A (MM105) from 2026-03-01 MCC01/01/DG1/E, B (MM332) from 2026-06-15 MCC02/02, C (MM306) from 2026-09-10
// status D, each with its message's reference; today is 2026-10-13, so C's row is today's: MCC02/02/DG1/D/NRN-hash-mm306.
// An MM301 arriving later states DG2 from 2026-05-01 under NRN-hash-mm301. Neither B nor C stated a DUoS group, so
// today's row takes DG2; both stated a reference, so today's keeps C's.
public class WhenABackdatedMeterPointStatementArrives
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterPointValues> _meterPoint = new();

    public WhenABackdatedMeterPointStatementArrives()
    {
        _meterPoint.Record(new MeterPointStatementBuilder().Build(), Today);
        _meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 6, 15)).WithCodes("MCC02", "02", null, null).WithNetworksReferenceNumber("NRN-hash-mm332").Build(), Today);
        _meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 9, 10)).WithCodes(null, null, null, "D").WithNetworksReferenceNumber("NRN-hash-mm306").Build(), Today);

        _meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 5, 1)).WithCodes(null, null, "DG2", null).WithNetworksReferenceNumber("NRN-hash-mm301").Build(), Today);
    }

    [Fact]
    public void KeepsWhatTheMm301StatedAsItsOwnHistoryRow() =>
        _meterPoint.History.Should().ContainInOrder(
            new Dated<MeterPointValues>(new DateOnly(2026, 3, 1), new MeterPointValues("MCC01", "01", "DG1", "E", "NRN-hash-mm105")),
            new Dated<MeterPointValues>(new DateOnly(2026, 5, 1), new MeterPointValues(null, null, "DG2", null, "NRN-hash-mm301")),
            new Dated<MeterPointValues>(new DateOnly(2026, 6, 15), new MeterPointValues("MCC02", "02", null, null, "NRN-hash-mm332")));

    [Fact]
    public void GivesTodaysRowTheBackdatedDuosGroupAndKeepsItsOwnReference() =>
        _meterPoint.Current.Should().Equal(
            new Dated<MeterPointValues>(new DateOnly(2026, 9, 10), new MeterPointValues("MCC02", "02", "DG2", "D", "NRN-hash-mm306")));
}
