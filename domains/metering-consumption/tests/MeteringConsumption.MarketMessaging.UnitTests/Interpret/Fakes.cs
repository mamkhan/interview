using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using BuildingBlocks.Messaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using MeteringConsumption.MarketMessaging.Domain;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

internal sealed class CallLog
{
    public List<string> Calls { get; } = [];
}

internal sealed class FakeArchiveReader(CallLog log, Func<Stream> open) : IInterpretArchiveBlobStore
{
    public Exception? Throws { get; set; }

    public Task<Stream> OpenAsync(string blobPath, CancellationToken cancellationToken)
    {
        log.Calls.Add("open");
        return Throws is null ? Task.FromResult(open()) : throw Throws;
    }
}

internal sealed class StubClassifier(string messageType) : IMessageClassifier
{
    public string Classify(Stream content) => messageType;
}

internal sealed class RefusingClassifier(string reason) : IMessageClassifier
{
    public string Classify(Stream content) => throw new MessageClassificationException(reason);
}

internal sealed class RecordingInterpreter(CallLog log, string market, string messageType, int readings) : IMessageStrategy
{
    public Exception? Throws { get; set; }

    public List<MarketMessageReceived> Interpreted { get; } = [];

    public string Market => market;

    public string MessageType => messageType;

    public Task<InterpretationOutcome> InterpretAsync(MarketMessageReceived receipt, Stream payload, CancellationToken cancellationToken)
    {
        log.Calls.Add("interpret");
        if (Throws is not null)
        {
            throw Throws;
        }

        Interpreted.Add(receipt);
        return Task.FromResult(new InterpretationOutcome(readings));
    }
}

internal sealed class RecordingOutcomePublisher(CallLog log) : IEventPublisher
{
    public List<(object Event, EventMetadata Metadata)> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

    public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
    {
        log.Calls.Add($"publish:{eventType.Name}");
        Published.Add((message, metadata));
        return Task.CompletedTask;
    }

    public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
