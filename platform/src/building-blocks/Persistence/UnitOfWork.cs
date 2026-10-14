using System.Data;
using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using BuildingBlocks.Seedwork;
using BuildingBlocks.Sql;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Persistence;

public sealed class UnitOfWork(string connectionString, IServiceProvider scope, TimeProvider time, IOutboxWriter outbox) : IUnitOfWork
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const int PrimaryKeyViolation = 2627;

    private readonly List<IAggregateRoot> _tracked = [];
    private SqlConnection? _connection;
    private SqlTransaction? _transaction;
    private bool _completed;

    public SqlConnection Connection
    {
        get
        {
            if (_connection is null)
            {
                _connection = new SqlConnection(connectionString);
                _connection.Open();
            }

            return _connection;
        }
    }

    public SqlTransaction? Transaction => _transaction;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("The unit of work has already begun.");
        }

        if (_connection is null)
        {
            _connection = new SqlConnection(connectionString);
            await _connection.OpenAsync(cancellationToken);
        }

        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public void Track(IAggregateRoot aggregate)
    {
        if (!_tracked.Contains(aggregate))
        {
            _tracked.Add(aggregate);
        }
    }

    public Task AddToOutboxAsync<TEvent>(TEvent contractEvent, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class =>
        outbox.AddAsync(Connection, RequireTransaction(), new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = typeof(TEvent).FullName!,
            Payload = JsonSerializer.Serialize(contractEvent, SerializerOptions),
            MessageId = metadata.MessageId,
            CorrelationId = metadata.CorrelationId,
            SessionId = metadata.SessionId,
            OccurredAtUtc = time.GetUtcNow(),
            State = OutboxMessage.StatePending,
        }, cancellationToken);

    public async Task<bool> TryMarkProcessedAsync(string consumer, string messageId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Consumer", consumer);
        parameters.Add("MessageId", messageId);
        parameters.AddUtc("ProcessedAtUtc", time.GetUtcNow());
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.ProcessedMessage (Consumer, MessageId, ProcessedAtUtc) VALUES (@Consumer, @MessageId, @ProcessedAtUtc);",
                parameters, RequireTransaction(), cancellationToken: cancellationToken));
            return true;
        }
        catch (SqlException exception) when (exception.Number == PrimaryKeyViolation)
        {
            return false;
        }
    }

    // A repeat with the same id blocks on the first request's row lock until that request commits or rolls
    // back, so a concurrent duplicate sees either the recorded result or a clean slate, never a half-run.
    public async Task<string?> TryBeginClientRequestAsync(Guid requestId, string commandName, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", requestId);
        parameters.Add("Name", commandName);
        parameters.AddUtc("ReceivedAtUtc", time.GetUtcNow());
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.ClientRequest (Id, Name, ReceivedAtUtc, ResultJson) VALUES (@Id, @Name, @ReceivedAtUtc, NULL);",
                parameters, RequireTransaction(), cancellationToken: cancellationToken));
            return null;
        }
        catch (SqlException exception) when (exception.Number == PrimaryKeyViolation)
        {
            var recorded = await Connection.QuerySingleAsync<string?>(new CommandDefinition(
                "SELECT ResultJson FROM dbo.ClientRequest WHERE Id = @Id;", new { Id = requestId }, RequireTransaction(),
                cancellationToken: cancellationToken));
            return recorded ?? "";
        }
    }

    public Task CompleteClientRequestAsync(Guid requestId, string resultJson, CancellationToken cancellationToken) =>
        Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.ClientRequest SET ResultJson = @ResultJson WHERE Id = @Id;", new { Id = requestId, ResultJson = resultJson },
            RequireTransaction(), cancellationToken: cancellationToken));

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = RequireTransaction();
        await new DomainEventDispatcher(scope).DispatchAsync(_tracked, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            if (!_completed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private SqlTransaction RequireTransaction() =>
        _transaction ?? throw new InvalidOperationException("The unit of work has not begun; call BeginAsync before writing.");
}
