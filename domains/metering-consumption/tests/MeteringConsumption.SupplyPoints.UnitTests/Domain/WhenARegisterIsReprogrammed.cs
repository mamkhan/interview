using AwesomeAssertions;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Domain;

// Register 001 of meter 90000320 is held from 2026-09-30 recording timeslot 01D with multiplier 1 (the real MM320
// sample); today is 2026-11-25. An MM332 keeps the meter on 2026-11-20 with the register now recording 24H with
// multiplier 10 and read 412 (type A, reason 04), and no decimal details. Today's row is 2026-11-20's, with the
// decimal details carried from before.
public class WhenARegisterIsReprogrammed
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private readonly KeyRows<RegisterValues> _register = new();

    public WhenARegisterIsReprogrammed()
    {
        _register.Record(new RegisterStatementBuilder().Build(), Today);
        _register.Record(new RegisterStatementBuilder()
            .StartingOn(new DateOnly(2026, 11, 20))
            .WithDetails(new RegisterDetails { TimeslotCode = "24H", RegisterTypeCode = "78", UomCode = "KWH", MeterMultiplier = 10m })
            .WithRead(new RegisterRead(412m, "A", "04", null, null))
            .Build(), Today);
    }

    [Fact]
    public void GivesTodaysRowTheNewDetailsAndReadWithTheDecimalDetailsItLeftOut() =>
        _register.Current.Should().Equal(new Dated<RegisterValues>(new DateOnly(2026, 11, 20), new RegisterValues(
            new RegisterDetails
            {
                TimeslotCode = "24H",
                RegisterTypeCode = "78",
                UomCode = "KWH",
                MeterMultiplier = 10m,
                PreDecimalDetails = "07",
                PostDecimalDetails = "0",
            },
            new RegisterRead(412m, "A", "04", null, null))));
}
