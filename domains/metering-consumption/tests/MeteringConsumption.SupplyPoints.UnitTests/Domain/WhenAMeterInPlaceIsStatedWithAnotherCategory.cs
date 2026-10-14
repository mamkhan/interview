using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Meter 90003311 is in place from 2026-09-16 as category RM106 (the real MM331 sample); today is 2026-10-13. A message on
// 2026-10-01 states the same serial as category RM107 and nothing else: the same meter with its category corrected. Its
// row from 2026-10-01 is today's and carries the location and reference the correction leaves out.
public class WhenAMeterInPlaceIsStatedWithAnotherCategory
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<MeterDetails> _meter = new();

    public WhenAMeterInPlaceIsStatedWithAnotherCategory()
    {
        _meter.Record(new MeterStatementBuilder().Build(), Today);
        _meter.Record(new MeterStatementBuilder().StartingOn(new DateOnly(2026, 10, 1)).WithDetails("RM107", null, null).Build(), Today);
    }

    [Fact]
    public void KeepsOnlyTheCategoryAsTheCorrectionsHistory() =>
        _meter.History.Should().Equal(
            new Dated<MeterDetails>(new DateOnly(2026, 9, 16), new MeterDetails("RM106", "07", "000000000090003311")),
            new Dated<MeterDetails>(new DateOnly(2026, 10, 1), new MeterDetails("RM107", null, null)));

    [Fact]
    public void CarriesWhatTheCorrectionLeavesOutIntoTodaysRow() =>
        _meter.Current.Should().Equal(
            new Dated<MeterDetails>(new DateOnly(2026, 10, 1), new MeterDetails("RM107", "07", "000000000090003311")));
}
