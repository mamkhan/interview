namespace MeteringConsumption.Consumption.Domain;

// A supply point's recent read days, the current version of each. The first forecast day, the anchor, is the day
// after the latest read. A day with no reading is absent, never zero.
public sealed class ConsumptionWindow
{
    private ConsumptionWindow(IReadOnlyList<ReadDay> days)
    {
        Days = days;
    }

    public IReadOnlyList<ReadDay> Days { get; }

    public DateOnly LatestReadDate => Days[^1].ReadDate;

    public DateOnly Anchor => LatestReadDate.AddDays(1);

    public static ConsumptionWindow Of(IEnumerable<ReadDay> days)
    {
        var ordered = days.OrderBy(day => day.ReadDate).ToArray();
        if (ordered.Length == 0)
        {
            throw new ForecastInputRejectedException("A forecast needs at least one read day.");
        }

        if (ordered.Select(day => day.ReadDate).Distinct().Count() != ordered.Length)
        {
            throw new ForecastInputRejectedException("A read date appears twice in the window.");
        }

        return new(ordered);
    }

    // The baseline M is the oldest read date from PufMaxDays − 1 to PufMinDays days before the anchor. M itself
    // is not counted: the own use is what the meter used on the read days after it.
    public PufWindow? FindPufWindow(ForecastSettings settings)
    {
        var from = Anchor.AddDays(-(settings.PufMaxDays - 1));
        var to = Anchor.AddDays(-settings.PufMinDays);
        var baseline = Days.FirstOrDefault(day => day.ReadDate >= from && day.ReadDate <= to);
        if (baseline is null)
        {
            return null;
        }

        var slotTotals = new decimal[ReadDay.SlotsPerDay];
        foreach (var day in Days.Where(day => day.ReadDate > baseline.ReadDate))
        {
            for (var slot = 0; slot < ReadDay.SlotsPerDay; slot++)
            {
                slotTotals[slot] += day.Slots[slot];
            }
        }

        return new PufWindow(baseline.ReadDate, LatestReadDate, slotTotals);
    }
}

public sealed record PufWindow(DateOnly Baseline, DateOnly LatestReadDate, IReadOnlyList<decimal> SlotTotals)
{
    public DateOnly FirstCountedDay => Baseline.AddDays(1);

    public int Days => LatestReadDate.DayNumber - Baseline.DayNumber;

    public decimal TotalKwh => SlotTotals.Sum();

    public decimal NightKwh => SlotTotals.Where((_, slot) => !ReadDay.IsDaySlot(slot)).Sum();

    public decimal DayKwh => SlotTotals.Where((_, slot) => ReadDay.IsDaySlot(slot)).Sum();
}
