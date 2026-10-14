namespace MeteringConsumption.Consumption.Domain;

// One usage factor a market message states for a supply point: kWh a year for a timeslot, estimated or
// actual, taking effect from a date. Source is the content hash of the sealed file it was parsed from.
public sealed record UsageFactorStatement(
    UsageFactorSlot Slot,
    DateOnly EffectiveFrom,
    decimal Value,
    string Source)
{
    public static UsageFactorStatement Of(
        string timeslot, DateOnly effectiveFrom, decimal? estimated, decimal? actual, string source)
    {
        if (!UsageFactorTimeslots.All.Contains(timeslot))
        {
            throw new ProfileStatementRejectedException(
                $"The usage factor timeslot '{timeslot}' is not one of {string.Join(", ", UsageFactorTimeslots.All)}.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ProfileStatementRejectedException($"The {timeslot} usage factor has no source.");
        }

        return (estimated, actual) switch
        {
            ({ } value, null) => new(new UsageFactorSlot(timeslot, UsageFactorKind.Estimated), effectiveFrom, value, source),
            (null, { } value) => new(new UsageFactorSlot(timeslot, UsageFactorKind.Actual), effectiveFrom, value, source),
            (null, null) => throw new ProfileStatementRejectedException(
                $"The {timeslot} usage factor effective {effectiveFrom:yyyy-MM-dd} states neither an estimate nor an actual."),
            _ => throw new ProfileStatementRejectedException(
                $"The {timeslot} usage factor effective {effectiveFrom:yyyy-MM-dd} states both an estimate and an actual."),
        };
    }
}
