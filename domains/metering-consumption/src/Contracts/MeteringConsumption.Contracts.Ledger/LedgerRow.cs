using Parquet.Serialization.Attributes;

namespace MeteringConsumption.Contracts.Ledger;

/// <summary>One half-hour of one leg of one ledger entry. A row describes itself: the provenance points back to its
/// source, and fuel, market and producer tell one producer's rows from another's.</summary>
public sealed class LedgerRow
{
    public const string ReversalLeg = "Reversal";
    public const string ReadingLeg = "Reading";

    /// <summary>The entry type of a first entry, the name Record's loader parses.</summary>
    public const string ReadingReceived = "ReadingReceived";

    public string LedgerEntryId { get; set; } = "";

    public string SupplyPointId { get; set; } = "";

    public string MeterSerial { get; set; } = "";

    public DateOnly ReadDate { get; set; }

    public string EntryType { get; set; } = "";

    public int Rrvn { get; set; }

    public int? SupersedesRrvn { get; set; }

    public string Leg { get; set; } = "";

    [ParquetTimestamp(ParquetTimestampResolution.Milliseconds, useLogicalTimestamp: true)]
    public DateTime PostedAtUtc { get; set; }

    public int Period { get; set; }

    [ParquetDecimal(18, 6)]
    public decimal Kwh { get; set; }

    public string Status { get; set; } = "";

    public int MeteringIntervalMinutes { get; set; }

    public string UomCode { get; set; } = "";

    public string SourceMessageType { get; set; } = "";

    [ParquetTimestamp(ParquetTimestampResolution.Milliseconds, useLogicalTimestamp: true)]
    public DateTime MarketTimestamp { get; set; }

    public string BlobPath { get; set; } = "";

    public string ContentHash { get; set; } = "";

    public string ReceivedFileName { get; set; } = "";

    public string Fuel { get; set; } = "";

    public string Market { get; set; } = "";

    public string Producer { get; set; } = "";
}
