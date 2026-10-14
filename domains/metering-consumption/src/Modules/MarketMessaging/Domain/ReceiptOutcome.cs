namespace MeteringConsumption.MarketMessaging.Domain;

public sealed record ReceiptOutcome
{
    public string? MessageType { get; init; }

    public required string Outcome { get; init; }

    public string? Interpreter { get; init; }

    public int? ReadingsPublished { get; init; }

    public string? FailureReason { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}
