namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class UsageFactors
{
    public decimal? ActualUsageFactor { get; set; }

    public DateOnly? EffectiveFromDate { get; set; }

    public decimal? EstimatedUsageFactor { get; set; }

    public string? TimeslotCode { get; set; }
}
