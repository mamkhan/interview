namespace BuildingBlocks.Seedwork;

/// <summary>Base for objects without identity: equal when every equality component is equal.
/// Prefer records; use this where equality needs components a record's positional shape cannot express.</summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(GetType().GetHashCode(), (hash, c) => HashCode.Combine(hash, c));
}
