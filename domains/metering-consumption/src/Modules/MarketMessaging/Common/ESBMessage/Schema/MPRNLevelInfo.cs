namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class MPRNLevelInfo
{
    public string? AlertFlag { get; set; }

    public string? MPRN { get; set; }

    public DateOnly? ReadDate { get; set; }

    public int? ReadingReplacementVersionNumber { get; set; }

    public IReadOnlyList<MeterID>? MeterIDs { get; set; }
}
