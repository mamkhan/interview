namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>A supply point's meter configuration as one market message states it, shared by every kind of event about
/// it: the meter point codes (null when not stated), the usage factors in file order and the meters listed.</summary>
public sealed record MeterConfiguration
{
    public string? MeterConfigurationCode { get; init; }

    public string? LoadProfileCode { get; init; }

    public string? DuosGroup { get; init; }

    public string? MeterPointStatusCode { get; init; }

    public required IReadOnlyList<UsageFactor> UsageFactors { get; init; }

    public required IReadOnlyList<ConfiguredMeter> Meters { get; init; }
}
