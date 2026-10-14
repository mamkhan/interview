using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// A register's details and its latest read. The read is one value: a statement states all of it or none of it, and a
// new read is a change like a new detail.
public sealed record RegisterValues(RegisterDetails Details, RegisterRead? Read) : ICarriedValues<RegisterValues>
{
    public static RegisterValues None { get; } = new(RegisterDetails.None, null);

    public RegisterValues CarriedFrom(RegisterValues? previous) => new(Details.CarriedFrom(previous?.Details), Read ?? previous?.Read);

    public RegisterValues NotStatedIn(RegisterValues other) =>
        new(Details.NotStatedIn(other.Details), other.Read is null ? Read : null);
}
