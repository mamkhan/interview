using BuildingBlocks.Sql;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Outbox;

public sealed class DapperOutboxWriter : IOutboxWriter
{
    private const string Sql = """
        INSERT INTO dbo.OutboxMessage (Id, EventType, Payload, CorrelationId, MessageId, SessionId, OccurredAtUtc, State, PublishedAtUtc)
        VALUES (@Id, @EventType, @Payload, @CorrelationId, @MessageId, @SessionId, @OccurredAtUtc, @State, NULL);
        """;

    public Task AddAsync(SqlConnection connection, SqlTransaction transaction, OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", message.Id);
        parameters.Add("EventType", message.EventType);
        parameters.Add("Payload", message.Payload);
        parameters.Add("CorrelationId", message.CorrelationId);
        parameters.Add("MessageId", message.MessageId);
        parameters.Add("SessionId", message.SessionId);
        parameters.AddUtc("OccurredAtUtc", message.OccurredAtUtc);
        parameters.Add("State", OutboxMessage.StatePending);
        return connection.ExecuteAsync(new CommandDefinition(Sql, parameters, transaction,
            cancellationToken: cancellationToken));
    }
}
