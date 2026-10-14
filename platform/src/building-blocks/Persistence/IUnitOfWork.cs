using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Persistence;

/// <summary>One message or request, one connection, one transaction, one module's tables, scoped per
/// delivery. A query uses <see cref="Connection"/> without beginning; a command begins, works and commits,
/// and the commit dispatches the tracked domain events first so their effects land in the same transaction.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Opened on first use. Without <see cref="BeginAsync"/> it serves reads only.</summary>
    SqlConnection Connection { get; }

    /// <summary>Null until <see cref="BeginAsync"/>; Dapper and EF Core enlist through it.</summary>
    SqlTransaction? Transaction { get; }

    Task BeginAsync(CancellationToken cancellationToken);

    /// <summary>Registers an aggregate whose domain events are dispatched before commit. Stores call this
    /// on load and save; a handler that constructs a new aggregate calls it directly.</summary>
    void Track(IAggregateRoot aggregate);

    /// <summary>Writes the event as a pending outbox row inside the transaction; the relay publishes it
    /// after commit.</summary>
    Task AddToOutboxAsync<TEvent>(TEvent contractEvent, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class;

    /// <summary>The inbox: records that this consumer has handled this message. False means it already
    /// had, inside a committed transaction, and the caller skips the work.</summary>
    Task<bool> TryMarkProcessedAsync(string consumer, string messageId, CancellationToken cancellationToken);

    /// <summary>Client-request idempotency for commands that arrive over HTTP with an <c>x-requestid</c>: the
    /// first call records the request and returns null; a repeat returns the result the first one
    /// recorded, or waits on the row lock until the first commits.</summary>
    Task<string?> TryBeginClientRequestAsync(Guid requestId, string commandName, CancellationToken cancellationToken);

    Task CompleteClientRequestAsync(Guid requestId, string resultJson, CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}
