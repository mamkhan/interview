namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>One register of a listed meter, or a smart meter's channel, which has no sequence number, timeslot or
/// read.</summary>
public sealed record ConfiguredRegister
{
    public string? MeterRegisterSequence { get; init; }

    public string? TimeslotCode { get; init; }

    public required string RegisterTypeCode { get; init; }

    public required string UOMCode { get; init; }

    public decimal? MeterMultiplier { get; init; }

    public int? MeteringInterval { get; init; }

    public string? PreDecimalDetails { get; init; }

    public string? PostDecimalDetails { get; init; }

    public RegisterRead? Read { get; init; }
}
