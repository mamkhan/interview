using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace LocalBlobEvents;

// One event per blob version (name and ETag), remembered for the process lifetime; the event id is derived
// from that pair so a re-announcement of the same version carries the same id, as Event Grid's would.
public sealed class BlobEventPoller(
    BlobServiceClient blobs,
    ServiceBusClient bus,
    IOptions<BlobEventsOptions> options,
    ILogger<BlobEventPoller> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly HashSet<string> _announced = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var container = blobs.GetBlobContainerClient(options.Value.Container);
        await using var sender = bus.CreateSender(options.Value.Queue);
        await container.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        logger.LogInformation("Announcing blobs created in {Container} on {Queue}", container.Name, options.Value.Queue);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var blob in container.GetBlobsAsync(cancellationToken: stoppingToken))
                {
                    var version = $"{container.Name}/{blob.Name}/{blob.Properties.ETag}";
                    if (!_announced.Add(version))
                    {
                        continue;
                    }

                    var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(version)).AsSpan(0, 16)).ToString();
                    var body = Event(id, container, blob.Name, blob.Properties.CreatedOn ?? DateTimeOffset.UtcNow);
                    await sender.SendMessageAsync(new ServiceBusMessage(body) { MessageId = id }, stoppingToken);
                    logger.LogInformation("BlobCreated {EventId} for {BlobName}", id, blob.Name);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Poll of {Container} failed; retrying", container.Name);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private static string Event(string id, BlobContainerClient container, string blobName, DateTimeOffset createdOn) =>
        JsonSerializer.Serialize(new
        {
            id,
            source = container.Uri.GetLeftPart(UriPartial.Authority),
            specversion = "1.0",
            type = "Microsoft.Storage.BlobCreated",
            subject = $"/blobServices/default/containers/{container.Name}/blobs/{blobName}",
            time = createdOn.ToString("O"),
            data = new
            {
                api = "PutBlob",
                blobType = "BlockBlob",
                url = container.GetBlobClient(blobName).Uri.ToString(),
            },
        });
}
