namespace MeteringConsumption.Consumption.Domain;

// The meter configuration one market message states for a supply point: up to four codes that take effect
// together from one date; a message leaves empty the codes it does not carry. Source is the content hash of the
// sealed file the message was parsed from.
public sealed record MeterConfigurationStatement
{
    public string? MeterConfigurationCode { get; init; }

    public string? LoadProfileCode { get; init; }

    public string? DuosGroup { get; init; }

    public string? MeterPointStatusCode { get; init; }

    public required DateOnly EffectiveFrom { get; init; }

    public required string Source { get; init; }

    public required string MessageType { get; init; }
}
