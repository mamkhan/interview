using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A (MM105, reference NRN-hash-mm105) from 2026-03-01 is held; today is 2026-10-13. ESB states the same codes for the
// same date in another file under reference NRN-hash-resent. The reference is a value like the codes, so it is a
// same-date correction: the one processed last wins.
public class WhenTheSameMeterPointStatementArrivesInAnotherFile
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterPointValues> _meterPoint = new();

    private readonly Recorded<MeterPointValues> _recorded;

    public WhenTheSameMeterPointStatementArrivesInAnotherFile()
    {
        _meterPoint.Record(new MeterPointStatementBuilder().Build(), Today);

        _recorded = _meterPoint.Record(new MeterPointStatementBuilder().WithNetworksReferenceNumber("NRN-hash-resent").Build(), Today);
    }

    [Fact]
    public void KeepsTheLaterReferenceOnTheDatesHistoryRowAndTodaysRow()
    {
        var corrected = new Dated<MeterPointValues>(new DateOnly(2026, 3, 1), new MeterPointValues("MCC01", "01", "DG1", "E", "NRN-hash-resent"));

        _recorded.Kind.Should().Be(RecordOutcomeKind.Replaced);
        _meterPoint.History.Should().Equal(corrected);
        _meterPoint.Current.Should().Equal(corrected);
    }
}
