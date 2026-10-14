namespace BuildingBlocks.Seedwork;

/// <summary>One failed rule on one property of a command.</summary>
public sealed record ValidationFailure(string Property, string Message);

/// <summary>A command whose shape fails its validator before any work starts. Deterministic: a consumer
/// dead-letters the message, an endpoint answers 400 with the failures by property.</summary>
public sealed class CommandValidationException(string commandName, IReadOnlyList<ValidationFailure> failures)
    : Exception($"{commandName} failed validation: {string.Join("; ", failures.Select(f => $"{f.Property}: {f.Message}"))}")
{
    public string CommandName { get; } = commandName;

    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;
}
