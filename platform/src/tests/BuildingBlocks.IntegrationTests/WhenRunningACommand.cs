using AwesomeAssertions;
using BuildingBlocks.Commands;
using BuildingBlocks.Persistence;
using BuildingBlocks.Seedwork;
using BuildingBlocks.TestsShared;
using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.IntegrationTests;

// The runner's promise, on the real database: validate, begin, guard, handle, commit, and nothing
// partial ever lands. The handler under test writes a row through the unit of work so the rollback is
// proved on a table, not on a fixture.
public sealed class WhenRunningACommand : IClassFixture<WhenRunningACommand.Database>, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ServiceProvider _provider;

    public WhenRunningACommand(Database database)
    {
        _connectionString = database.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_connectionString);
        services.AddCommandRunner();
        services.AddScoped<ICommandHandler<WriteRow, string>, WriteRowHandler>();
        services.AddScoped<IValidator<WriteRow>, WriteRowValidator>();
        _provider = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task LeavesNoRowAndNoInboxMarkerWhenTheHandlerThrowsAfterWriting()
    {
        var command = new WriteRow(Hash("throws"), ThrowAfterWrite: true);

        // The scope ends with the delivery, as it does in a host; disposing the unit of work is the rollback.
        var act = () => RunOnceInFreshScopeAsync(command, MessageKey.For<WriteRowHandler>("m-throws"));

        await act.Should().ThrowAsync<HandlerFailed>();
        (await CountAsync("dbo.WrittenRow", "ContentHash", command.ContentHash)).Should().Be(0);
        (await CountAsync("dbo.ProcessedMessage", "MessageId", "m-throws")).Should().Be(0);
    }

    [Fact]
    public async Task RunsTheHandlerOnceForARedeliveredMessage()
    {
        var command = new WriteRow(Hash("redelivered"), ThrowAfterWrite: false);
        var key = MessageKey.For<WriteRowHandler>("m-redelivered");

        var first = await RunOnceInFreshScopeAsync(command, key);
        var second = await RunOnceInFreshScopeAsync(command, key);

        first.Disposition.Should().Be(RunDisposition.Ran);
        first.Outcome.Should().Be("written");
        second.Disposition.Should().Be(RunDisposition.AlreadyProcessed);
        (await CountAsync("dbo.WrittenRow", "ContentHash", command.ContentHash)).Should().Be(1);
        (await CountAsync("dbo.ProcessedMessage", "MessageId", "m-redelivered")).Should().Be(1);
    }

    [Fact]
    public async Task ReturnsTheFirstResultForARepeatedClientRequestWithoutRunningAgain()
    {
        var command = new WriteRow(Hash("client"), ThrowAfterWrite: false);
        var requestId = Guid.NewGuid();

        var first = await RunIdempotentInFreshScopeAsync(command, requestId);
        var second = await RunIdempotentInFreshScopeAsync(command, requestId);

        first.Disposition.Should().Be(RunDisposition.Ran);
        second.Disposition.Should().Be(RunDisposition.Repeated);
        second.Outcome.Should().Be("written");
        (await CountAsync("dbo.WrittenRow", "ContentHash", command.ContentHash)).Should().Be(1);
    }

    [Fact]
    public async Task NeverBeginsACommandThatFailsValidation()
    {
        var command = new WriteRow("not-a-hash", ThrowAfterWrite: false);

        var act = () => RunOnceInFreshScopeAsync(command, MessageKey.For<WriteRowHandler>("m-invalid"));

        var thrown = await act.Should().ThrowAsync<CommandValidationException>();
        thrown.Which.Failures.Should().ContainSingle(f => f.Property == nameof(WriteRow.ContentHash));
        (await CountAsync("dbo.ProcessedMessage", "MessageId", "m-invalid")).Should().Be(0);
    }

    private async Task<RunResult<string>> RunOnceInFreshScopeAsync(WriteRow command, MessageKey key)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommandRunner>()
            .RunOnceAsync<WriteRow, string>(command, key, TestContext.Current.CancellationToken);
    }

    private async Task<RunResult<string>> RunIdempotentInFreshScopeAsync(WriteRow command, Guid requestId)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CommandRunner>()
            .RunIdempotentAsync<WriteRow, string>(command, requestId, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountAsync(string table, string column, string value)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {column} = @value", new { value });
    }

    // Offline: printf '%s' "<name>" | shasum -a 256, zero-padded here to 64 hex characters by construction.
    private static string Hash(string name) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name)));

    public sealed record WriteRow(string ContentHash, bool ThrowAfterWrite);

    private sealed class WriteRowValidator : AbstractValidator<WriteRow>
    {
        public WriteRowValidator()
        {
            RuleFor(c => c.ContentHash).Length(64);
        }
    }

    private sealed class HandlerFailed() : Exception("the handler failed after writing");

    private sealed class WriteRowHandler(IUnitOfWork unitOfWork) : ICommandHandler<WriteRow, string>
    {
        public async Task<string> HandleAsync(WriteRow command, CancellationToken cancellationToken)
        {
            await unitOfWork.Connection.ExecuteAsync(
                new CommandDefinition("INSERT INTO dbo.WrittenRow (ContentHash) VALUES (@ContentHash)",
                    new { command.ContentHash }, unitOfWork.Transaction, cancellationToken: cancellationToken));

            return command.ThrowAfterWrite ? throw new HandlerFailed() : "written";
        }
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenRunningACommand), PlatformSchema.CreateAsync);
}
