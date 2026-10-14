using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.MarketMessaging;

public sealed record UsageFactorsReceived : EventEnvelope, IEvent
{
    public required string MPRN { get; init; }

    public string? NetworksReferenceNumber { get; init; }

    public required DateOnly ReadDate { get; init; }

    public required IReadOnlyList<UsageFactor> UsageFactors { get; init; }

    public required ReadingProvenance Provenance { get; init; }

    public string ReadingId => $"{Provenance.ContentHash}:{MPRN}:{ReadDate:yyyy-MM-dd}";
}
