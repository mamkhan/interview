namespace MeteringConsumption.Contracts.Events.MarketMessaging;

// An operator file has been sealed in the archive and a receipt exists for its arrival. Carries no message
// type: the Interpret feature classifies the file. Published with the receipt id as message id, so a redelivered
// arrival is one receipt.
public sealed record MarketMessageReceived : EventEnvelope
{
    public required string ReceiptId { get; init; }

    public required string Market { get; init; }

    public required string Fuel { get; init; }

    public required string FileName { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required string Hash { get; init; }

    public required string BlobPath { get; init; }
}
