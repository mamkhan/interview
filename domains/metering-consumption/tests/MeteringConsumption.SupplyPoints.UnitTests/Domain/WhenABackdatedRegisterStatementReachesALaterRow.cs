using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Register 001 of meter 90000320 is held from 2026-09-30 with multiplier 1 and read 0 (the real MM320 sample), and an
// MM332 on 2026-11-20 restated only its timeslot, type, unit and the read 12345; today is 2026-11-25, so today's row is
// 2026-11-20's. A backdated message states multiplier 10 from 2026-10-15 with the same timeslot, type and unit. Today's
// row takes the multiplier, which no later statement stated, and keeps its own type, unit and read.
public class WhenABackdatedRegisterStatementReachesALaterRow
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private readonly KeyRows<RegisterValues> _register = new();

    public WhenABackdatedRegisterStatementReachesALaterRow()
    {
        _register.Record(new RegisterStatementBuilder().Build(), Today);
        _register.Record(new RegisterStatementBuilder()
            .StartingOn(new DateOnly(2026, 11, 20))
            .WithDetails(new RegisterDetails { TimeslotCode = "01D", RegisterTypeCode = "78", UomCode = "KWH" })
            .WithRead(new RegisterRead(12345m, "A", "04", null, null))
            .Build(), Today);

        _register.Record(new RegisterStatementBuilder()
            .StartingOn(new DateOnly(2026, 10, 15))
            .WithDetails(new RegisterDetails { TimeslotCode = "01D", RegisterTypeCode = "78", UomCode = "KWH", MeterMultiplier = 10m })
            .WithRead(null)
            .Build(), Today);
    }

    [Fact]
    public void GivesTodaysRowTheMultiplierAndKeepsItsOwnTypeUnitAndRead() =>
        _register.Current.Should().Equal(new Dated<RegisterValues>(new DateOnly(2026, 11, 20), new RegisterValues(
            new RegisterDetails
            {
                TimeslotCode = "01D",
                RegisterTypeCode = "78",
                UomCode = "KWH",
                MeterMultiplier = 10m,
                PreDecimalDetails = "07",
                PostDecimalDetails = "0",
            },
            new RegisterRead(12345m, "A", "04", null, null))));
}
