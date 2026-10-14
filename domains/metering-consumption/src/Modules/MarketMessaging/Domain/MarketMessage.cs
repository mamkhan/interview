namespace MeteringConsumption.MarketMessaging.Domain;

public sealed class MarketMessage
{
    public required string ReceiptId { get; init; }

    public required string Market { get; init; }

    public required string Fuel { get; init; }

    public required string FileName { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required string Hash { get; init; }

    public required string BlobPath { get; init; }

    public required string CorrelationId { get; init; }

    public string? MessageType { get; init; }

    public string? Outcome { get; init; }

    public string? Interpreter { get; init; }

    public int? ReadingsPublished { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }
}
