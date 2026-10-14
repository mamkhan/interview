namespace MeteringConsumption.Consumption.Domain;

// One read date's current version as the kWh of each of the day's 48 local half-hour slots, slot 0 starting at
// midnight. A slot no period covers holds zero.
public sealed record ReadDay
{
    public const int SlotsPerDay = 48;

    private ReadDay(DateOnly readDate, decimal[] slots)
    {
        ReadDate = readDate;
        Slots = slots;
    }

    public DateOnly ReadDate { get; }

    public IReadOnlyList<decimal> Slots { get; }

    public decimal TotalKwh => Slots.Sum();

    public static ReadDay Of(DateOnly readDate, IReadOnlyList<decimal> slots) =>
        slots.Count == SlotsPerDay
            ? new(readDate, [.. slots])
            : throw new ForecastInputRejectedException($"A read day has {SlotsPerDay} slots, not {slots.Count}.");

    // Periods carry the operator's ordinal by time. Clocks go forward at 01:00 on the last Sunday of March, so
    // that day has no 01:00 hour: its 46 periods skip slots 2 and 3. They go back at 02:00 on the last Sunday of
    // October, so that day has 01:00 twice: periods 5 and 6 repeat slots 2 and 3 and are added to them. Any other
    // count fills slots by rank and drops what is past 48, as V1 does.
    public static ReadDay FromPeriods(DateOnly readDate, IEnumerable<EntryPeriod> periods)
    {
        var ordered = periods.OrderBy(period => period.Period).Select(period => period.Kwh).ToArray();
        var slots = new decimal[SlotsPerDay];
        if (ordered.Length == 46 && IsLastSunday(readDate, 3))
        {
            ordered[..2].CopyTo(slots, 0);
            ordered[2..].CopyTo(slots, 4);
        }
        else if (ordered.Length == 50 && IsLastSunday(readDate, 10))
        {
            ordered[..4].CopyTo(slots, 0);
            slots[2] += ordered[4];
            slots[3] += ordered[5];
            ordered[6..].CopyTo(slots, 4);
        }
        else
        {
            ordered.Take(SlotsPerDay).ToArray().CopyTo(slots, 0);
        }

        return new(readDate, slots);
    }

    public static bool IsDaySlot(int slot) => slot is >= 16 and <= 45;

    private static bool IsLastSunday(DateOnly date, int month) =>
        date.Month == month && date.DayOfWeek == DayOfWeek.Sunday && date.AddDays(7).Month != month;
}
