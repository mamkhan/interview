using System.Diagnostics.Metrics;

namespace MeteringConsumption.Consumption.Application.Features.Record;

public static class RecordTelemetry
{
    public const string MeterName = ConsumptionMeter.Name;

    public const string ConflictingResendsName = "consumption.record.conflicting_resends";

    private static readonly Meter Meter = new(MeterName);

    // Non-zero means one version reached Record with two sets of values: operations look at the warning, and a
    // second meter or channel on one supply-point day is the case to rule out first.
    private static readonly Counter<long> ConflictingResends = Meter.CreateCounter<long>(
        ConflictingResendsName, "{reading}", "Readings whose version was already kept with other values.");

    public static void RecordConflictingResend() => ConflictingResends.Add(1);
}
