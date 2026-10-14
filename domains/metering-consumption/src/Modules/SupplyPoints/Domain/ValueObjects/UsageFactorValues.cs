using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

public sealed record UsageFactorValues(decimal? Estimated, decimal? Actual) : ICarriedValues<UsageFactorValues>
{
    public static UsageFactorValues None { get; } = new(null, null);

    // A message may state only the estimate or only the actual (an exchange's closing read states the actual), so a
    // value it leaves out stays what it was.
    public UsageFactorValues CarriedFrom(UsageFactorValues? previous) => previous is null
        ? this
        : new UsageFactorValues(Estimated ?? previous.Estimated, Actual ?? previous.Actual);

    public UsageFactorValues NotStatedIn(UsageFactorValues other) =>
        new(other.Estimated is null ? Estimated : null, other.Actual is null ? Actual : null);
}
