using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;

public interface IMarketMessageRepository
{
    // False when the receipt is already there: a redelivery of the same arrival.
    Task<bool> AddAsync(MarketMessage receipt, CancellationToken cancellationToken);

    // False when no row carries the receipt id yet: the outcome overtook the receipt and must wait.
    Task<bool> RecordOutcomeAsync(string receiptId, ReceiptOutcome outcome, CancellationToken cancellationToken);
}
