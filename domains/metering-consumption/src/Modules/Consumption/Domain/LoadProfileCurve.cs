namespace MeteringConsumption.Consumption.Domain;

public enum CurveBand
{
    Flat,
    Day,
    Night,
}

// One hour of a load profile on one date, as a share of a typical year's use: the whole-day curve and the day and
// night bands. A profile states only the curves it has.
public sealed record LoadProfileHour(DateOnly Date, int Hour, decimal? Flat, decimal? Day, decimal? Night);

// A load profile's hourly values over a run of dates. A question about a date or hour it does not hold, or about a
// band it does not state, has no answer rather than a zero.
public sealed class LoadProfileCurve
{
    private const int HoursPerDay = 24;

    private readonly Dictionary<(DateOnly Date, int Hour), LoadProfileHour> _hours;

    public LoadProfileCurve(string code, IEnumerable<LoadProfileHour> hours)
    {
        Code = code;
        _hours = hours.ToDictionary(hour => (hour.Date, hour.Hour));
    }

    public string Code { get; }

    // The 24 values of each date from the first, day after day; null when any is missing.
    public IReadOnlyList<decimal>? Hours(DateOnly first, int days, CurveBand band)
    {
        var values = new decimal[days * HoursPerDay];
        for (var day = 0; day < days; day++)
        {
            for (var hour = 0; hour < HoursPerDay; hour++)
            {
                if (Value(first.AddDays(day), hour, band) is not { } value)
                {
                    return null;
                }

                values[(day * HoursPerDay) + hour] = value;
            }
        }

        return values;
    }

    public decimal? Sum(DateOnly first, int days, CurveBand band) => Hours(first, days, band)?.Sum();

    private decimal? Value(DateOnly date, int hour, CurveBand band) =>
        _hours.TryGetValue((date, hour), out var value)
            ? band switch
            {
                CurveBand.Flat => value.Flat,
                CurveBand.Day => value.Day,
                _ => value.Night,
            }
            : null;
}
