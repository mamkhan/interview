namespace MeteringConsumption.Contracts.Queries;

/// <summary>The meter point's codes and the usage factors in effect at one supply point on one day. A code no message has
/// stated is null.</summary>
public sealed record MeterPointConfiguration
{
    public required string MPRN { get; init; }

    public required DateOnly Day { get; init; }

    public string? MeterConfigurationCode { get; init; }

    public string? LoadProfileCode { get; init; }

    public string? DuosGroup { get; init; }

    public string? MeterPointStatusCode { get; init; }

    public required IReadOnlyList<UsageFactorInEffect> UsageFactors { get; init; }
}

/// <summary>One timeslot's usage factors in effect on the day.</summary>
public sealed record UsageFactorInEffect(string TimeslotCode, decimal? EstimatedUsageFactor, decimal? ActualUsageFactor);
