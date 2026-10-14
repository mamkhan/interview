namespace BuildingBlocks.Outbox;

public sealed class OutboxMessage
{
    public const int StatePending = 0;
    public const int StatePublished = 1;

    public Guid Id { get; set; }

    public string EventType { get; set; } = "";

    public string Payload { get; set; } = "";

    public string CorrelationId { get; set; } = "";

    public string MessageId { get; set; } = "";

    public string? SessionId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public int State { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }
}
