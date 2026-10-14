namespace MeteringConsumption.Contracts.Events;

// The outcome values a receipt can carry, on the bus and in the receipt table.
public static class MarketMessageOutcomes
{
    public const string Processed = "processed";

    public const string NoInterpreter = "no-interpreter";

    public const string Failed = "failed";
}
