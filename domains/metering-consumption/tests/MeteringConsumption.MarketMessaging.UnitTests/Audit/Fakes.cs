using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.UnitTests.Audit;

// The receipt table as the repository's two answers describe it: an insert is refused for a receipt id already there,
// and an outcome is refused until its receipt is. A receipt may be set to land only once the outcome has asked a
// given number of times, as when the outcome overtakes it on the bus.
internal sealed class FakeMarketMessageRepository : IMarketMessageRepository
{
    private readonly Dictionary<string, int> _landsOnAttempt = [];

    public Dictionary<string, MarketMessage> Receipts { get; } = [];

    public Dictionary<string, ReceiptOutcome> Outcomes { get; } = [];

    public int OutcomeAttempts { get; private set; }

    public void ReceiptLandsOnAttempt(string receiptId, int attempt) => _landsOnAttempt[receiptId] = attempt;

    public Task<bool> AddAsync(MarketMessage receipt, CancellationToken cancellationToken) =>
        Task.FromResult(Receipts.TryAdd(receipt.ReceiptId, receipt));

    public Task<bool> RecordOutcomeAsync(string receiptId, ReceiptOutcome outcome, CancellationToken cancellationToken)
    {
        OutcomeAttempts++;
        var there = Receipts.ContainsKey(receiptId)
            || (_landsOnAttempt.TryGetValue(receiptId, out var attempt) && OutcomeAttempts >= attempt);
        if (there)
        {
            Outcomes[receiptId] = outcome;
        }

        return Task.FromResult(there);
    }
}
