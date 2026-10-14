namespace BuildingBlocks.Seedwork;

/// <summary>Reacts to a domain event inside the unit of work that raised it, before commit: it may change
/// another aggregate of the module or add an outbox row through the scoped unit of work, and a throw
/// rolls the whole unit back. Registered explicitly by the module that owns the event type.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
