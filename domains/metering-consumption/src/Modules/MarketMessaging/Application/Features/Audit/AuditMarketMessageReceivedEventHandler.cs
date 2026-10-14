using MeteringConsumption.MarketMessaging.Domain;
using BuildingBlocks.Messaging;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using Microsoft.Extensions.Logging;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.MarketMessaging.Application.Features.Audit;

// Requirement one: every arrival is a row, written by its own subscription so processing can neither
// block nor lose it. The insert is idempotent by receipt id, so a redelivery needs no inbox.
public sealed class AuditMarketMessageReceivedEventHandler(
    IMarketMessageRepository repository,
    ILogger<AuditMarketMessageReceivedEventHandler> logger) : IEventHandler<MarketMessageReceived>
{
    public async Task HandleAsync(MarketMessageReceived message, CancellationToken cancellationToken)
    {
        var inserted = await repository.AddAsync(new MarketMessage
        {
            ReceiptId = message.ReceiptId,
            Market = message.Market,
            Fuel = message.Fuel,
            FileName = message.FileName,
            ReceivedAt = message.ReceivedAt,
            Hash = message.Hash,
            BlobPath = message.BlobPath,
            CorrelationId = message.CorrelationId,
        }, cancellationToken);

        logger.LogInformation("Receipt {ReceiptId} for {FileName} {Disposition}", message.ReceiptId, message.FileName,
            inserted ? "recorded" : "already recorded");
    }
}
