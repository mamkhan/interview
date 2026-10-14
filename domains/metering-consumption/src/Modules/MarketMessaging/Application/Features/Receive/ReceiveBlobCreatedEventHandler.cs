using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Domain;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.MarketMessaging.Application.Features.Receive;

// Seal first, announce second, delete last: the dropped file is the commit marker, so a crash anywhere
// before the delete redelivers the event and the same receipt id makes the redelivery a no-op downstream.
public sealed class ReceiveBlobCreatedEventHandler(
    IDropBlobStore drop,
    IReceiveArchiveBlobStore archive,
    IEventPublisher publisher,
    IOptions<DropOptions> options,
    TimeProvider time,
    ILogger<ReceiveBlobCreatedEventHandler> logger) : IEventHandler<BlobCreated>
{
    public async Task HandleAsync(BlobCreated message, CancellationToken cancellationToken)
    {
        var blobName = message.BlobName;
        var content = await drop.OpenAsync(blobName, cancellationToken);
        if (content is null)
        {
            logger.LogInformation("Dropped file {BlobName} is already gone; receipt {ReceiptId} was sealed by an earlier delivery",
                blobName, message.Id);
            return;
        }

        var market = options.Value.Market;
        var sealedFile = SealedFile.Of(market, content.ToMemory().Span);
        var blobPath = await archive.StoreAsync(sealedFile, content.ToMemory(), blobName, message.Time, cancellationToken);

        var receipt = new MarketMessageReceived
        {
            EventId = DeterministicGuid.From(message.Id),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = time.GetUtcNow(),
            CorrelationId = message.Id,
            ReceiptId = message.Id,
            Market = market,
            Fuel = options.Value.Fuel,
            FileName = blobName,
            ReceivedAt = message.Time,
            Hash = sealedFile.Hash,
            BlobPath = blobPath,
        };

        await publisher.PublishAsync(receipt, new EventMetadata
        {
            MessageId = message.Id,
            CorrelationId = message.Id,
            ApplicationProperties = new Dictionary<string, string>
            {
                [MeteringConsumptionTopology.MessageProperties.Market] = market,
            },
        }, cancellationToken);

        await drop.DeleteAsync(blobName, cancellationToken);
        logger.LogInformation("Sealed {FileName} as receipt {ReceiptId} at {BlobPath}", blobName, message.Id, blobPath);
    }
}
