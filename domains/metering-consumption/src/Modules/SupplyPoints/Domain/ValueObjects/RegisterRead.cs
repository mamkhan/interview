namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// A read ESB states with a register.
public sealed record RegisterRead(decimal ReadingValue, string? ReadTypeCode, string? ReadReasonCode, string? ReadStatusCode,
    DateOnly? PreviousReadDate);
