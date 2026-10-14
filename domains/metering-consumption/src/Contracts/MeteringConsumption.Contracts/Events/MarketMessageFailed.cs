namespace MeteringConsumption.Contracts.Events;

// A receipt the Interpret feature could not interpret deterministically: unclassifiable, or refused by its
// interpreter. A business outcome, not a dead letter; the reason is recorded on the receipt.
public sealed record MarketMessageFailed : EventEnvelope
{
    public required string ReceiptId { get; init; }

    public string? MessageType { get; init; }

    public required string Reason { get; init; }
}
