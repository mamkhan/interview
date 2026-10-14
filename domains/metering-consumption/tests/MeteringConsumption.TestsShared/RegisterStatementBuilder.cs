using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.TestsShared;

// Defaults to register 001 of meter 90000320 in the real MM320 sample ESB_MM320_10900000320: timeslot 01D, register type 78,
// KWH, multiplier 1, 7 digits before the decimal point and none after, read 0 (type A, reason 26) on 2026-09-30.
public sealed class RegisterStatementBuilder
{
    private DateOnly _startDate = new(2026, 9, 30);
    private RegisterDetails _details = new()
    {
        TimeslotCode = "01D",
        RegisterTypeCode = "78",
        UomCode = "KWH",
        MeterMultiplier = 1m,
        PreDecimalDetails = "07",
        PostDecimalDetails = "0",
    };
    private RegisterRead? _read = new(0m, "A", "26", null, null);

    public RegisterStatementBuilder StartingOn(DateOnly startDate)
    {
        _startDate = startDate;
        return this;
    }

    public RegisterStatementBuilder WithDetails(RegisterDetails details)
    {
        _details = details;
        return this;
    }

    public RegisterStatementBuilder WithRead(RegisterRead? read)
    {
        _read = read;
        return this;
    }

    // A smart meter's channel from the real MM331 sample ESB_MM331_10900000331: meter 90003311 on 2026-09-16, no sequence
    // number, no timeslot and no read.
    public RegisterStatementBuilder AsAChannel(string registerTypeCode, string uomCode, int meteringInterval)
    {
        _startDate = new DateOnly(2026, 9, 16);
        _details = new RegisterDetails { RegisterTypeCode = registerTypeCode, UomCode = uomCode, MeteringInterval = meteringInterval };
        _read = null;
        return this;
    }

    public Dated<RegisterValues> Build() => new(_startDate, new RegisterValues(_details, _read));
}
