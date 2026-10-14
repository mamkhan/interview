namespace BuildingBlocks.Seedwork;

/// <summary>What the unit of work needs from any aggregate it tracks: the events raised since the last
/// dequeue, so they can be dispatched inside the transaction before commit.</summary>
public interface IAggregateRoot
{
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();
}
