namespace BuildingBlocks.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class;

    /// <summary>Publish by the event's runtime type, for a caller holding events through a shared interface.</summary>
    Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken);

    /// <summary>Publish an already-serialised payload by event type name; the outbox relay's path.</summary>
    Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken);
}
