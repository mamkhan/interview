using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.MarketMessaging;

/// <summary>A change to one supply point's meter configuration, as one market message states it, published whole so a
/// reader stores all of it or none. Its meter point and meters take effect on the effective date, or on the read date
/// when the message states none; each usage factor on its own date.</summary>
public sealed record MeterConfigurationChangeReceived : EventEnvelope, IEvent
{
    public required string MPRN { get; init; }

    public string? NetworksReferenceNumber { get; init; }

    public DateOnly? EffectiveFromDate { get; init; }

    public DateOnly? ReadDate { get; init; }

    public required MeterConfiguration Configuration { get; init; }

    public required ReadingProvenance Provenance { get; init; }

    public string ReadingId => $"{Provenance.ContentHash}:{MPRN}:meter-configuration-change";
}
