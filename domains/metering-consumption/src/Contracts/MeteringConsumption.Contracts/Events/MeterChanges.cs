namespace MeteringConsumption.Contracts.Events;

/// <summary>How a market message lists a meter: already in place (a change of supplier), newly fitted, or kept through
/// a technical change.</summary>
public static class MeterChanges
{
    public const string InPlace = "in-place";

    public const string New = "new";

    public const string Retained = "retained";
}
