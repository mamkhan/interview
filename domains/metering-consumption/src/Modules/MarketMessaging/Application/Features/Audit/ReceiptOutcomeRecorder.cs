using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Application.Features.Audit;

// The receipt and its outcome arrive on independent subscriptions, so the outcome may overtake the row.
// Redelivery after an abandon is immediate, so ten attempts would pass in under a second; the recorder
// therefore waits a bounded few seconds for the row before it throws, and the throw is a transient
// failure that redelivers the message rather than a dead letter.
public sealed class ReceiptOutcomeRecorder(
    IMarketMessageRepository repository,
    TimeProvider time,
    ILogger<ReceiptOutcomeRecorder> logger)
{
    private static readonly TimeSpan[] WaitsForTheReceipt =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    public async Task RecordAsync(string receiptId, ReceiptOutcome outcome, CancellationToken cancellationToken)
    {
        foreach (var wait in WaitsForTheReceipt)
        {
            if (await repository.RecordOutcomeAsync(receiptId, outcome, cancellationToken))
            {
                logger.LogInformation("Receipt {ReceiptId} outcome {Outcome} recorded", receiptId, outcome.Outcome);
                return;
            }

            await Task.Delay(wait, time, cancellationToken);
        }

        if (await repository.RecordOutcomeAsync(receiptId, outcome, cancellationToken))
        {
            logger.LogInformation("Receipt {ReceiptId} outcome {Outcome} recorded", receiptId, outcome.Outcome);
            return;
        }

        throw new ReceiptNotYetRecordedException(receiptId);
    }
}

