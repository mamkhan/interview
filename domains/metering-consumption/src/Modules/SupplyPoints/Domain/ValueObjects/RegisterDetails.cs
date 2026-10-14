using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// What a register is: the timeslot and units it records and how its reads are written. A smart meter's channel states
// only its register type, unit and metering interval. Every statement of a register states its type and unit.
public sealed record RegisterDetails : ICarriedValues<RegisterDetails>
{
    public static RegisterDetails None { get; } = new();

    public string? TimeslotCode { get; init; }

    public string? RegisterTypeCode { get; init; }

    public string? UomCode { get; init; }

    public decimal? MeterMultiplier { get; init; }

    public int? MeteringInterval { get; init; }

    public string? PreDecimalDetails { get; init; }

    public string? PostDecimalDetails { get; init; }

    // A retained meter's register may leave out its decimal details, so what a message leaves out stays what it was.
    public RegisterDetails CarriedFrom(RegisterDetails? previous) => previous is null
        ? this
        : new RegisterDetails
        {
            TimeslotCode = TimeslotCode ?? previous.TimeslotCode,
            RegisterTypeCode = RegisterTypeCode ?? previous.RegisterTypeCode,
            UomCode = UomCode ?? previous.UomCode,
            MeterMultiplier = MeterMultiplier ?? previous.MeterMultiplier,
            MeteringInterval = MeteringInterval ?? previous.MeteringInterval,
            PreDecimalDetails = PreDecimalDetails ?? previous.PreDecimalDetails,
            PostDecimalDetails = PostDecimalDetails ?? previous.PostDecimalDetails,
        };

    public RegisterDetails NotStatedIn(RegisterDetails other) => new()
    {
        TimeslotCode = other.TimeslotCode is null ? TimeslotCode : null,
        RegisterTypeCode = other.RegisterTypeCode is null ? RegisterTypeCode : null,
        UomCode = other.UomCode is null ? UomCode : null,
        MeterMultiplier = other.MeterMultiplier is null ? MeterMultiplier : null,
        MeteringInterval = other.MeteringInterval is null ? MeteringInterval : null,
        PreDecimalDetails = other.PreDecimalDetails is null ? PreDecimalDetails : null,
        PostDecimalDetails = other.PostDecimalDetails is null ? PostDecimalDetails : null,
    };
}
