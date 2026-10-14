namespace MeteringConsumption.Contracts;

/// <summary>Fields every published event carries: identity, publisher, time, the correlation identifier
/// minted at the workflow's entry point, and the optional AsOf a consumer runs its clock on when set.</summary>
public abstract record EventEnvelope
{
    public required Guid EventId { get; init; }

    public required string PublishingContext { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }

    public DateTimeOffset? AsOf { get; init; }
}
