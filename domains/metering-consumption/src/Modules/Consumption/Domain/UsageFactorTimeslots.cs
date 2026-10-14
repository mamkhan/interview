namespace MeteringConsumption.Consumption.Domain;

// The timeslots ESB defines for usage factors: the 24-hour rate, the day and night rates, and the three
// rates of the 01 set. The profile has a column for each, estimated and actual, so a code outside this set
// is refused rather than dropped.
public static class UsageFactorTimeslots
{
    public static IReadOnlyList<string> All { get; } = ["24H", "00D", "00N", "01D", "01N", "01P"];
}
