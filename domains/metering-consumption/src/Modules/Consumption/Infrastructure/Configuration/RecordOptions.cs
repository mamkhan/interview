using System.ComponentModel.DataAnnotations;

namespace MeteringConsumption.Consumption.Infrastructure.Configuration;

public sealed class RecordOptions
{
    public const string Section = "Record";

    // The connection the host's ledger blob client is registered under, beside the archive's.
    public const string LedgerConnectionName = "ledger";

    // Replicas times sessions is the concurrency the ledger account sees.
    [Range(1, 256)]
    public int MaxConcurrentSessions { get; set; } = 64;
}
