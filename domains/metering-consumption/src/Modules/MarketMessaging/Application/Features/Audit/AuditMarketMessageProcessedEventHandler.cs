using MeteringConsumption.MarketMessaging.Domain;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;

namespace MeteringConsumption.MarketMessaging.Application.Features.Audit;

public sealed class AuditMarketMessageProcessedEventHandler(ReceiptOutcomeRecorder recorder)
    : IEventHandler<MarketMessageProcessed>
{
    public Task HandleAsync(MarketMessageProcessed message, CancellationToken cancellationToken) =>
        recorder.RecordAsync(message.ReceiptId, new ReceiptOutcome
        {
            MessageType = message.MessageType,
            Outcome = message.Outcome,
            Interpreter = message.Interpreter,
            ReadingsPublished = message.ReadingsPublished,
            ProcessedAt = message.OccurredAtUtc,
        }, cancellationToken);
}
