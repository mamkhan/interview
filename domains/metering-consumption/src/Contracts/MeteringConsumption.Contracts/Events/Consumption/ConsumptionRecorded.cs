using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.Consumption;

/// <summary>One version of a meter-day kept in the consumption ledger: the new version in full and, for a
/// replacement, the version it cancels, negated. A consumer applies both legs; together they net to the change.
/// Published with the ledger entry id as message id and the supply point as session id.</summary>
public sealed record ConsumptionRecorded : EventEnvelope
{
    public required string LedgerEntryId { get; init; }

    public required string SupplyPointId { get; init; }

    public required string MeterSerial { get; init; }

    public required DateOnly ReadDate { get; init; }

    public required int RRVN { get; init; }

    public int? SupersedesRRVN { get; init; }

    /// <summary><c>ReadingReceived</c> for a first version, <c>ReplacementReadingReceived</c> for a later one.</summary>
    public required string EntryType { get; init; }

    public required DateTimeOffset PostedAtUtc { get; init; }

    public required int MeteringIntervalMinutes { get; init; }

    public required string UOMCode { get; init; }

    public required string Fuel { get; init; }

    public LedgerLeg? Reversal { get; init; }

    public required LedgerLeg Reading { get; init; }

    public required ReadingProvenance Provenance { get; init; }
}
