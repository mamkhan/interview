using BuildingBlocks.Seedwork;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Persistence;

/// <summary>Dequeues the tracked aggregates' domain events and invokes every registered
/// <see cref="IDomainEventHandler{TEvent}"/> in the scope, looping until no handler raises a further
/// event; the round limit turns an endless chain into a failure instead of a hang.</summary>
public sealed class DomainEventDispatcher(IServiceProvider scope)
{
    public const int MaxRounds = 8;

    public async Task<int> DispatchAsync(IReadOnlyCollection<IAggregateRoot> aggregates, CancellationToken cancellationToken)
    {
        var dispatched = 0;
        for (var round = 0; round < MaxRounds; round++)
        {
            var events = aggregates.SelectMany(a => a.DequeueDomainEvents()).ToList();
            if (events.Count == 0)
            {
                return dispatched;
            }

            foreach (var domainEvent in events)
            {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                foreach (var handler in scope.GetServices(handlerType))
                {
                    await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
                    dispatched++;
                }
            }
        }

        throw new InvalidOperationException(
            $"Domain event handlers did not settle within {MaxRounds} rounds; a handler is raising an event that re-triggers it.");
    }
}
