using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// A meter with no registers; today is 2026-10-13. The real MM320 sample states register 001 of meter 90000320 on
// 2026-09-30: timeslot 01D, register type 78, KWH, multiplier 1, pre-decimal 07, post-decimal 0, and the change of
// supplier read 0 (type A, reason 26).
public class WhenARegisterIsFirstStated
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly KeyRows<RegisterValues> _register = new();

    public WhenARegisterIsFirstStated()
    {
        _register.Record(new RegisterStatementBuilder().Build(), Today);
    }

    [Fact]
    public void KeepsItsDetailsAndItsReadAsHistoryAndAsTodaysRow()
    {
        var stated = new Dated<RegisterValues>(new DateOnly(2026, 9, 30), new RegisterValues(
            new RegisterDetails
            {
                TimeslotCode = "01D",
                RegisterTypeCode = "78",
                UomCode = "KWH",
                MeterMultiplier = 1m,
                PreDecimalDetails = "07",
                PostDecimalDetails = "0",
            },
            new RegisterRead(0m, "A", "26", null, null)));

        _register.History.Should().Equal(stated);
        _register.Current.Should().Equal(stated);
    }
}
