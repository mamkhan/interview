using BuildingBlocks.Messaging;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.UnitTests.Receive;

// One log shared by the fakes, so a test can assert the order of the hops.
internal sealed class CallLog
{
    public List<string> Calls { get; } = [];
}

internal sealed class FakeDropContainer(CallLog log) : IDropBlobStore
{
    public Dictionary<string, byte[]> Blobs { get; } = [];

    public List<string> Deleted { get; } = [];

    public Task<BinaryData?> OpenAsync(string blobName, CancellationToken cancellationToken)
    {
        log.Calls.Add("open");
        return Task.FromResult(Blobs.TryGetValue(blobName, out var bytes) ? new BinaryData(bytes) : null);
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        log.Calls.Add("delete");
        Deleted.Add(blobName);
        Blobs.Remove(blobName);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingArchive(CallLog log) : IReceiveArchiveBlobStore
{
    public List<(SealedFile File, string FileName, DateTimeOffset ReceivedAt)> Stored { get; } = [];

    public Exception? Throws { get; set; }

    public Task<string> StoreAsync(SealedFile sealedFile, ReadOnlyMemory<byte> content, string fileName,
        DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        log.Calls.Add("archive");
        if (Throws is not null)
        {
            throw Throws;
        }

        Stored.Add((sealedFile, fileName, receivedAt));
        return Task.FromResult(sealedFile.Path);
    }
}

internal sealed class RecordingPublisher(CallLog log) : IEventPublisher
{
    public List<(object Event, EventMetadata Metadata)> Published { get; } = [];

    public Exception? Throws { get; set; }

    public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class => PublishAsync(message, typeof(TEvent), metadata, cancellationToken);

    public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken)
    {
        log.Calls.Add("publish");
        if (Throws is not null)
        {
            throw Throws;
        }

        Published.Add((message, metadata));
        return Task.CompletedTask;
    }

    public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
