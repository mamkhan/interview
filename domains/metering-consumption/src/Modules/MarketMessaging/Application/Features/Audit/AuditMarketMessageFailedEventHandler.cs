using MeteringConsumption.MarketMessaging.Domain;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;

namespace MeteringConsumption.MarketMessaging.Application.Features.Audit;

public sealed class AuditMarketMessageFailedEventHandler(ReceiptOutcomeRecorder recorder)
    : IEventHandler<MarketMessageFailed>
{
    public Task HandleAsync(MarketMessageFailed message, CancellationToken cancellationToken) =>
        recorder.RecordAsync(message.ReceiptId, new ReceiptOutcome
        {
            MessageType = message.MessageType,
            Outcome = MarketMessageOutcomes.Failed,
            FailureReason = message.Reason,
            ProcessedAt = message.OccurredAtUtc,
        }, cancellationToken);
}
