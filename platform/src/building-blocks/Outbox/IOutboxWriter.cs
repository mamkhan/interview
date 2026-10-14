using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Outbox;

/// <summary>Writes an outbox row inside the caller's transaction: the state change and the pending
/// event commit or roll back together, which is what makes state-plus-publish atomic.</summary>
public interface IOutboxWriter
{
    Task AddAsync(SqlConnection connection, SqlTransaction transaction, OutboxMessage message,
        CancellationToken cancellationToken);
}
