using System.Diagnostics.Metrics;

namespace BuildingBlocks.Messaging;

/// <summary>Per-message telemetry every consumer records: how long a message waited
/// to be picked up, how long handling took, and how often handling failed.</summary>
public static class MessagingTelemetry
{
    public const string MeterName = "Acme.Messaging";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> HandlingDuration = Meter.CreateHistogram<double>(
        "messaging.handling.duration", "s", "Time spent in the handler for one message.");

    private static readonly Histogram<double> QueueWait = Meter.CreateHistogram<double>(
        "messaging.queue.wait", "s", "Time between a message being enqueued and picked up.");

    private static readonly Counter<long> Failures = Meter.CreateCounter<long>(
        "messaging.handling.failures", "{message}", "Handler invocations that threw.");

    public static void RecordQueueWait(string entity, TimeSpan wait) =>
        QueueWait.Record(wait.TotalSeconds, new KeyValuePair<string, object?>("messaging.entity", entity));

    public static void RecordHandled(string entity, TimeSpan duration) =>
        HandlingDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("messaging.entity", entity));

    public static void RecordFailure(string entity) =>
        Failures.Add(1, new KeyValuePair<string, object?>("messaging.entity", entity));
}
