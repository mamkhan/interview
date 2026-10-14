using AwesomeAssertions;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using BuildingBlocks.TestsShared;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.IntegrationTests;

public sealed class WhenRelayingAnOutboxMessage(WhenRelayingAnOutboxMessage.Database database)
    : IClassFixture<WhenRelayingAnOutboxMessage.Database>
{
    private readonly string _connectionString = database.ConnectionString;

    [Fact]
    public async Task NeverPublishesARowWrittenInARolledBackTransaction()
    {
        var writer = new DapperOutboxWriter();
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = connection.BeginTransaction();
            await writer.AddAsync(connection, (SqlTransaction)transaction, Pending("rolled-back"),
                TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        (await CountAsync("rolled-back")).Should().Be(0);
    }

    [Fact]
    public async Task PublishesACommittedRowOnceAndMarksItPublished()
    {
        var writer = new DapperOutboxWriter();
        var pending = Pending("committed");
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = connection.BeginTransaction();
            await writer.AddAsync(connection, (SqlTransaction)transaction, pending,
                TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var publisher = new RecordingPublisher();
        var relay = new OutboxRelay(_connectionString, publisher, TimeProvider.System,
            NullLogger<OutboxRelay>.Instance);
        await relay.StartAsync(TestContext.Current.CancellationToken);
        OutboxMessage row;
        try
        {
            // The relay marks the row after the publish returns, so stopping it the moment the publisher
            // sees the message would cancel the mark mid-flight; wait for the row itself.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            do
            {
                await Task.Delay(200, TestContext.Current.CancellationToken);
                row = await ReadRowAsync(pending.Id);
            }
            while (row.State != OutboxMessage.StatePublished && DateTimeOffset.UtcNow < deadline);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }

        publisher.Published.Should().ContainSingle().Which.MessageId.Should().Be(pending.MessageId);
        row.State.Should().Be(OutboxMessage.StatePublished);
        row.PublishedAtUtc.Should().NotBeNull();
    }

    private async Task<OutboxMessage> ReadRowAsync(Guid id)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<OutboxMessage>(new CommandDefinition(
            "SELECT * FROM dbo.OutboxMessage WHERE Id = @Id;", new { Id = id },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<int> CountAsync(string correlationId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.OutboxMessage WHERE CorrelationId = @CorrelationId;",
            new { CorrelationId = correlationId }, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static OutboxMessage Pending(string correlationId) => new()
    {
        Id = Guid.CreateVersion7(),
        EventType = "MeteringConsumption.Contracts.MarketMessageReceived",
        Payload = """{"messageType":"MM343"}""",
        CorrelationId = correlationId,
        MessageId = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = DateTimeOffset.UtcNow,
        State = OutboxMessage.StatePending,
    };

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<EventMetadata> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
            where TEvent : class => throw new NotSupportedException();

        public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
            CancellationToken cancellationToken)
        {
            Published.Add(metadata);
            return Task.CompletedTask;
        }
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenRelayingAnOutboxMessage), PlatformSchema.CreateAsync);
}
