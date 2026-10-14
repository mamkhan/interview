namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>A meter a market message lists as in place, newly fitted or kept, with its registers or channels.</summary>
public sealed record ConfiguredMeter
{
    public required string SerialNumber { get; init; }

    public string? MeterCategoryCode { get; init; }

    public string? MeterLocationCode { get; init; }

    public string? ExchangedMeterReference { get; init; }

    /// <summary>One of <see cref="MeterChanges"/>.</summary>
    public required string Change { get; init; }

    public required IReadOnlyList<ConfiguredRegister> Registers { get; init; }
}
