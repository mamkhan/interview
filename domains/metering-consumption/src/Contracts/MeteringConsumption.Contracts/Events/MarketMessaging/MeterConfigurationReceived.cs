using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.MarketMessaging;

public sealed record MeterConfigurationReceived : EventEnvelope, IEvent
{
    public required string MPRN { get; init; }

    public string? MeterConfigurationCode { get; init; }

    public string? LoadProfileCode { get; init; }

    public string? DuosGroup { get; init; }

    public string? MeterPointStatusCode { get; init; }

    public DateOnly? EffectiveFromDate { get; init; }

    public DateOnly? ReadDate { get; init; }

    public string? NetworksReferenceNumber { get; init; }

    /// <summary>Whether V1 treats the meter as smart (its NSMP flag); null when the publisher does not state it,
    /// so a consumer keeps what it already holds.</summary>
    public bool? IsNsmp { get; init; }

    public required ReadingProvenance Provenance { get; init; }

    public string ReadingId => $"{Provenance.ContentHash}:{MPRN}:{EffectiveFromDate ?? ReadDate:yyyy-MM-dd}";
}
