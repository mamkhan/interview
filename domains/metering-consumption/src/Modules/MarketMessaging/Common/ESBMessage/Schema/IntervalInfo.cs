namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class IntervalInfo
{
    // DateTimeOffset, not DateTime: the values carry an offset, and on the October fallback day two
    // readings an hour apart parse to the same local DateTime and stop being orderable.
    public DateTimeOffset? IntervalPeriodTimestamp { get; set; }

    public string? IntervalStatusCode { get; set; }

    public decimal? IntervalValue { get; set; }
}
