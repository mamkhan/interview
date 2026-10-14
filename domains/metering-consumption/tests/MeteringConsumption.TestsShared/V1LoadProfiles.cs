using System.Globalization;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Real hourly load-profile values for LP1, LP2 and LP25 from 2026-04-21 to 2026-09-27, as the reference-data table
// holds them: V1's IH_LoadProfiles extract summed into hours, LP25 as V1's blended smart curve. Dumped once from the
// proof of concept's reference-data reader, whose forecasts reproduce V1's to four decimals.
public static class V1LoadProfiles
{
    private static readonly Lazy<Dictionary<string, LoadProfileCurve>> Curves = new(Load);

    public static readonly DateOnly FirstDate = new(2026, 4, 21);

    public static readonly DateOnly LastDate = new(2026, 9, 27);

    public static LoadProfileCurve? Curve(string code) => Curves.Value.GetValueOrDefault(code);

    private static Dictionary<string, LoadProfileCurve> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "v1-load-profile-hours.csv");
        return File.ReadLines(path)
            .Skip(1)
            .Select(line => line.Split(','))
            .GroupBy(fields => fields[0])
            .ToDictionary(
                profile => profile.Key,
                profile => new LoadProfileCurve(profile.Key, profile.Select(fields => new LoadProfileHour(
                    DateOnly.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    int.Parse(fields[2], CultureInfo.InvariantCulture),
                    Value(fields[3]),
                    Value(fields[4]),
                    Value(fields[5])))));
    }

    private static decimal? Value(string field) =>
        field.Length == 0 ? null : decimal.Parse(field, NumberStyles.Float, CultureInfo.InvariantCulture);
}
