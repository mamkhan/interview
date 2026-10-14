namespace BuildingBlocks.Seedwork;

/// <summary>An invariant the domain refuses to break. Deterministic: the same input fails the same way, so
/// a consumer dead-letters on first delivery and an endpoint answers 422. Expected business outcomes are
/// not exceptions; a handler returns them as typed results.</summary>
public abstract class DomainException(string message) : Exception(message);
