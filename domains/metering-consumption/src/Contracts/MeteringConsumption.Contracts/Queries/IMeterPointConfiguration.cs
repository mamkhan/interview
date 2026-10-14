namespace MeteringConsumption.Contracts.Queries;

/// <summary>What ESB states is in effect at a supply point on a day: the meter point's codes and each timeslot's usage
/// factors. SupplyPoints answers it; a module in the same host calls it.</summary>
public interface IMeterPointConfiguration
{
    /// <summary>Null when nothing is known to be in effect at the supply point on that day.</summary>
    Task<MeterPointConfiguration?> InEffectOnAsync(string mprn, DateOnly day, CancellationToken cancellationToken);
}
