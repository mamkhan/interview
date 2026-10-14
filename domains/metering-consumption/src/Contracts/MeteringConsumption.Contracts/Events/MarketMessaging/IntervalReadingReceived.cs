using MeteringConsumption.Contracts.Events.Abstraction;
using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.MarketMessaging;

public sealed record IntervalReadingReceived : EventEnvelope, IEvent
{
    public required string MPRN { get; init; }

    public required string MeterSerial { get; init; }

    public required DateOnly ReadDate { get; init; }

    public required int RRVN { get; init; }

    public required int MeteringIntervalMinutes { get; init; }

    public required string UOMCode { get; init; }

    public required IReadOnlyList<IntervalPeriod> Periods { get; init; }

    public required ReadingProvenance Provenance { get; init; }

    public string ReadingId => $"{Provenance.ContentHash}:{MPRN}:{ReadDate:yyyy-MM-dd}:{RRVN}";
}
