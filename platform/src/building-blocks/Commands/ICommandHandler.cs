namespace BuildingBlocks.Commands;

/// <summary>One use case's handler: takes the command, works through the scoped unit of work it receives by
/// injection, returns a typed outcome. Expected business results are outcomes; invariants throw
/// <c>DomainException</c>. The runner owns validation, the transaction and idempotency around it.</summary>
public interface ICommandHandler<in TCommand, TOutcome> where TCommand : class
{
    Task<TOutcome> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
