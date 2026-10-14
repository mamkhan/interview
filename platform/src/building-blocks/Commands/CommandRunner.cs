using System.Text.Json;
using BuildingBlocks.Persistence;
using BuildingBlocks.Seedwork;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Commands;

/// <summary>The pipeline a command slice runs through, in a fixed order: validate, begin the unit of work,
/// guard idempotency, run the handler, dispatch domain events, commit. One method, so a slice's execution
/// order is readable in one place and a failure at any step commits nothing.</summary>
public sealed class CommandRunner(IServiceProvider scope, IUnitOfWork unitOfWork, ILogger<CommandRunner> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A command with no idempotency guard of its own: the handler's writes must be idempotent by
    /// key, or the caller guards (a session, a natural key on the row).</summary>
    public async Task<TOutcome> RunAsync<TCommand, TOutcome>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
    {
        await ValidateAsync(command, cancellationToken);
        await unitOfWork.BeginAsync(cancellationToken);
        var outcome = await HandleAsync<TCommand, TOutcome>(command, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return outcome;
    }

    /// <summary>A message-triggered command: the inbox marker commits with the work, so a redelivery
    /// after commit is a no-op and a redelivery after a failure runs again.</summary>
    public async Task<RunResult<TOutcome>> RunOnceAsync<TCommand, TOutcome>(TCommand command, MessageKey key,
        CancellationToken cancellationToken) where TCommand : class
    {
        await ValidateAsync(command, cancellationToken);
        await unitOfWork.BeginAsync(cancellationToken);
        if (!await unitOfWork.TryMarkProcessedAsync(key.Consumer, key.MessageId, cancellationToken))
        {
            logger.LogInformation("Skipping redelivered {MessageId} for {Consumer}", key.MessageId, key.Consumer);
            return RunResult.AlreadyProcessed<TOutcome>();
        }

        var outcome = await HandleAsync<TCommand, TOutcome>(command, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return RunResult.Ran(outcome);
    }

    /// <summary>An HTTP command under an <c>x-requestid</c>: a repeat returns the first run's recorded outcome
    /// without running the handler again.</summary>
    public async Task<RunResult<TOutcome>> RunIdempotentAsync<TCommand, TOutcome>(TCommand command, Guid requestId,
        CancellationToken cancellationToken) where TCommand : class
    {
        await ValidateAsync(command, cancellationToken);
        await unitOfWork.BeginAsync(cancellationToken);
        var recorded = await unitOfWork.TryBeginClientRequestAsync(requestId, typeof(TCommand).Name, cancellationToken);
        if (recorded is not null)
        {
            logger.LogInformation("Returning the recorded result for repeated request {RequestId}", requestId);
            var replay = recorded.Length == 0 ? default : JsonSerializer.Deserialize<TOutcome>(recorded, SerializerOptions);
            return RunResult.Repeated(replay);
        }

        var outcome = await HandleAsync<TCommand, TOutcome>(command, cancellationToken);
        await unitOfWork.CompleteClientRequestAsync(requestId, JsonSerializer.Serialize(outcome, SerializerOptions), cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return RunResult.Ran(outcome);
    }

    private async Task ValidateAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in scope.GetServices<IValidator<TCommand>>())
        {
            var result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors.Select(e => new ValidationFailure(e.PropertyName, e.ErrorMessage)));
        }

        if (failures.Count > 0)
        {
            throw new CommandValidationException(typeof(TCommand).Name, failures);
        }
    }

    private async Task<TOutcome> HandleAsync<TCommand, TOutcome>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
    {
        var handler = scope.GetRequiredService<ICommandHandler<TCommand, TOutcome>>();
        using var activity = logger.BeginScope(new Dictionary<string, object?> { ["Command"] = typeof(TCommand).Name });
        var outcome = await handler.HandleAsync(command, cancellationToken);
        logger.LogInformation("{Command} handled with {Outcome}", typeof(TCommand).Name, outcome);
        return outcome;
    }
}

/// <summary>Whether the handler ran this time, and what it produced when it did or when it first did.</summary>
public sealed record RunResult<TOutcome>(RunDisposition Disposition, TOutcome? Outcome)
{
    public bool HandlerRan => Disposition == RunDisposition.Ran;
}

public static class RunResult
{
    public static RunResult<TOutcome> Ran<TOutcome>(TOutcome outcome) => new(RunDisposition.Ran, outcome);

    public static RunResult<TOutcome> AlreadyProcessed<TOutcome>() => new(RunDisposition.AlreadyProcessed, default);

    public static RunResult<TOutcome> Repeated<TOutcome>(TOutcome? recorded) => new(RunDisposition.Repeated, recorded);
}

public enum RunDisposition
{
    /// <summary>The handler ran and its work committed.</summary>
    Ran,

    /// <summary>The inbox already held this message for this consumer; nothing ran.</summary>
    AlreadyProcessed,

    /// <summary>The client request was already recorded; the first run's outcome is returned.</summary>
    Repeated,
}
