namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>A read ESB states with a register, as the message carries it.</summary>
public sealed record RegisterRead(decimal ReadingValue, string? ReadTypeCode, string? ReadReasonCode, string? ReadStatusCode,
    DateOnly? PreviousReadDate);
