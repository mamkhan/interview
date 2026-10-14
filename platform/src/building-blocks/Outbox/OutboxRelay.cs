using BuildingBlocks.Messaging;
using BuildingBlocks.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Outbox;

/// <summary>Relays pending outbox rows to the bus. A row that fails to publish stays pending and is
/// retried next cycle; consumers are idempotent by MessageId, so a crash between publish and
/// mark-published re-sends harmlessly.</summary>
public sealed class OutboxRelay(
    string connectionString,
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    private const string PendingSql = """
        SELECT TOP (@BatchSize) Id, EventType, Payload, CorrelationId, MessageId, SessionId, OccurredAtUtc, State, PublishedAtUtc
        FROM dbo.OutboxMessage
        WHERE State = @State
        ORDER BY OccurredAtUtc;
        """;

    private const string MarkPublishedSql = """
        UPDATE dbo.OutboxMessage SET State = @State, PublishedAtUtc = @PublishedAtUtc WHERE Id = @Id;
        """;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay cycle failed; retrying next cycle");
            }

            try
            {
                await Task.Delay(PollInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RelayPendingAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var pending = await connection.QueryAsync<OutboxMessage>(new CommandDefinition(PendingSql,
            new { BatchSize, State = OutboxMessage.StatePending }, cancellationToken: cancellationToken));

        foreach (var message in pending)
        {
            await publisher.PublishRawAsync(message.EventType, new BinaryData(message.Payload),
                new EventMetadata
                {
                    MessageId = message.MessageId,
                    CorrelationId = message.CorrelationId,
                    SessionId = message.SessionId,
                }, cancellationToken);

            var parameters = new DynamicParameters();
            parameters.Add("Id", message.Id);
            parameters.Add("State", OutboxMessage.StatePublished);
            parameters.AddUtc("PublishedAtUtc", time.GetUtcNow());
            await connection.ExecuteAsync(new CommandDefinition(MarkPublishedSql, parameters,
                cancellationToken: cancellationToken));
        }
    }
}
