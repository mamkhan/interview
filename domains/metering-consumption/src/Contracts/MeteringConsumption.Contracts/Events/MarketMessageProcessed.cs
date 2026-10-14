namespace MeteringConsumption.Contracts.Events;

// The Interpret feature's outcome for one receipt: interpreted, or a type no interpreter reads yet.
// Published with the receipt id as message id.
public sealed record MarketMessageProcessed : EventEnvelope
{
    public required string ReceiptId { get; init; }

    public required string MessageType { get; init; }

    // One of MarketMessageOutcomes: processed or no-interpreter.
    public required string Outcome { get; init; }

    public string? Interpreter { get; init; }

    public int ReadingsPublished { get; init; }
}
