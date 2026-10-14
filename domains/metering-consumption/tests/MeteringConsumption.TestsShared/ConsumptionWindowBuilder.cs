using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Read days for a forecast's window. A whole-day use goes in the first slot, a night use in slot 0 and a day use in
// slot 16, the first day slot; a day given by its 48 slots is taken as it is.
public sealed class ConsumptionWindowBuilder
{
    private readonly List<ReadDay> _days = [];

    public ConsumptionWindowBuilder DailyUse(DateOnly latestReadDate, int days, decimal kwhPerDay) =>
        DailyUse(latestReadDate, days, kwhPerDay, 0m);

    public ConsumptionWindowBuilder DailyUse(DateOnly latestReadDate, int days, decimal nightKwh, decimal dayKwh)
    {
        for (var day = 0; day < days; day++)
        {
            var slots = new decimal[ReadDay.SlotsPerDay];
            slots[0] = nightKwh;
            slots[16] = dayKwh;
            _days.Add(ReadDay.Of(latestReadDate.AddDays(-day), slots));
        }

        return this;
    }

    public ConsumptionWindowBuilder Day(DateOnly readDate, IReadOnlyList<decimal> slots)
    {
        _days.Add(ReadDay.Of(readDate, slots));
        return this;
    }

    public ConsumptionWindow Build() => ConsumptionWindow.Of(_days);
}
