namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>Where a reading came from: for a market file, enough to find the sealed file and re-derive the
/// reading from it; a source with no file leaves the blob path and file name empty.</summary>
public sealed record ReadingProvenance(
    string Market,
    string SourceMessageType,
    DateTimeOffset MarketTimestamp,
    string BlobPath,
    string ContentHash,
    string ReceivedFileName);
