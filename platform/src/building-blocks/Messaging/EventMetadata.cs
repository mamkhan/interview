namespace BuildingBlocks.Messaging;

/// <summary>Transport metadata the publisher stamps on a message: MessageId is the idempotency handle,
/// SessionId the ordering key that every publish to a session-enabled topic must set, and
/// ApplicationProperties are promoted onto the transport for correlation filters.</summary>
public sealed record EventMetadata
{
    public required string MessageId { get; init; }

    public required string CorrelationId { get; init; }

    public string? SessionId { get; init; }

    public IReadOnlyDictionary<string, string>? ApplicationProperties { get; init; }
}
