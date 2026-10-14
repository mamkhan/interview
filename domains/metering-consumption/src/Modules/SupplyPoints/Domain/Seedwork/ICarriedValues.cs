namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

// Values a statement may state only in part: a value it leaves out is null, and stays what it was.
public interface ICarriedValues<TSelf>
    where TSelf : class
{
    // What is in effect before anything is stated.
    static abstract TSelf None { get; }

    TSelf CarriedFrom(TSelf? previous);

    // The values this states that the other leaves out.
    TSelf NotStatedIn(TSelf other);
}
