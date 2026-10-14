namespace MeteringConsumption.Consumption.Domain;

// Immutable once posted. A replacement carries the version it cancels, negated, beside itself in full, so the
// entries of a meter-day always sum to its latest version.
public sealed record LedgerEntry
{
    public required string LedgerEntryId { get; init; }

    public required MeterDay MeterDay { get; init; }

    public required string MeterSerial { get; init; }

    public required EntryType EntryType { get; init; }

    public required int Rrvn { get; init; }

    public int? SupersedesRrvn { get; init; }

    public required DateTimeOffset PostedAtUtc { get; init; }

    public required int MeteringIntervalMinutes { get; init; }

    public required string UomCode { get; init; }

    public EntryLeg? Reversal { get; init; }

    public required EntryLeg Reading { get; init; }
}
