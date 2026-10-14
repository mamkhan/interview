using System.ComponentModel.DataAnnotations;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Infrastructure.Configuration;

public sealed class ForecastOptions
{
    public const string Section = "Forecast";

    [Range(1, 366)]
    public int HorizonDays { get; set; } = 62;

    [Range(1, 60)]
    public int PufMinDays { get; set; } = 7;

    [Range(2, 60)]
    public int PufMaxDays { get; set; } = 30;

    [Range(0, 1_000_000)]
    public decimal EufMinAcceptableKwh { get; set; } = 1050m;

    [Range(0, 1_000_000)]
    public decimal EufMaxAcceptableKwh { get; set; } = 16800m;

    [Range(1, 3650)]
    public int SupersededRetentionDays { get; set; } = 30;

    [Range(1, 100_000)]
    public int CleanupBatchSize { get; set; } = 1000;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    // Replicas times sessions is the concurrency the ledger account and the database see.
    [Range(1, 256)]
    public int MaxConcurrentSessions { get; set; } = 64;

    public ForecastSettings ToSettings() =>
        new(HorizonDays, PufMinDays, PufMaxDays, EufMinAcceptableKwh, EufMaxAcceptableKwh);
}
