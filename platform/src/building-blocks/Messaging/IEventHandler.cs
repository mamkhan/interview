namespace BuildingBlocks.Messaging;

/// <summary>What a slice implements to react to an event, independent of the transport.
/// <see cref="EventConsumer{TEvent}"/> is the Service Bus adapter that resolves it from a fresh DI scope
/// per delivery. Must be idempotent: the same event can arrive more than once by design.</summary>
public interface IEventHandler<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent message, CancellationToken cancellationToken);
}
