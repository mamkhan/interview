using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// The order the handler touched its collaborators in, across all of them.
internal sealed class CallLog
{
    public List<string> Calls { get; } = [];
}

// Holds entries the way the ledger container does, one per meter-day and RRVN, and can fail its next append the
// way a storage outage does: nothing of the entry is kept.
internal sealed class FakeLedgerStore(CallLog log) : IConsumptionLedgerStore
{
    private readonly Dictionary<(MeterDay, int), RecordedEntry> _entries = [];

    public int Appends { get; private set; }

    public bool FailNextAppend { get; set; }

    public IReadOnlyList<RecordedEntry> Kept => [.. _entries.Values.OrderBy(recorded => recorded.Entry.Rrvn)];

    public Task<IReadOnlyList<RecordedEntry>> LoadAsync(MeterDay meterDay, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RecordedEntry>>(
            [.. _entries.Where(kept => kept.Key.Item1 == meterDay).Select(kept => kept.Value).OrderBy(recorded => recorded.Entry.Rrvn)]);

    public Task AppendAsync(RecordedEntry recorded, CancellationToken cancellationToken)
    {
        log.Calls.Add("append");
        if (FailNextAppend)
        {
            FailNextAppend = false;
            throw new IOException("The ledger account is unavailable.");
        }

        _entries[(recorded.Entry.MeterDay, recorded.Entry.Rrvn)] = recorded;
        Appends++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPublisher(CallLog log) : IEventPublisher
{
    public List<(object Event, EventMetadata Metadata)> Published { get; } = [];

    public bool FailNextPublish { get; set; }

    public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class
    {
        log.Calls.Add("publish");
        if (FailNextPublish)
        {
            FailNextPublish = false;
            throw new TimeoutException("Service Bus did not answer.");
        }

        Published.Add((message, metadata));
        return Task.CompletedTask;
    }

    public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
}
