using Growth.Contracts;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// The meters the forecast tests use, as the two sources state them, and the settings the plan sets. The single-rate
// and day/night personas are the proof of concept's 10001234567 and 10003333333 (LP1 and LP2 by their MCC, DG1, no
// estimated usage factor); the area averages are V1's IH_AnnualAverageUsage 2026 row for DG1.
public static class ForecastPersonas
{
    public static readonly ForecastSettings Settings = new(62, 7, 30, 1050m, 16800m);

    public static readonly MeterPointFacts SingleRate = new("MCC01", "01", "DG1", "E", null, null, null);

    public static readonly MeterPointFacts DayNight = new("MCC02", "02", "DG1", "E", null, null, null);

    public static readonly TariffFacts NotTimeOfUse = new(false, false, null, null);

    public static readonly AnnualAverageUsage Dg1In2026 = new(3853m, 2945m, 1966m);

    public static TariffFacts NsmpTimeOfUse(string calcType, string effectiveLoadProfileCode) =>
        new(true, true, calcType, effectiveLoadProfileCode);

    public static TariffFacts NsmpTwentyFourHour(string effectiveLoadProfileCode) =>
        NsmpTimeOfUse(TariffCalcTypes.TwentyFourHour, effectiveLoadProfileCode);

    public static TariffFacts NsmpDayNight(string effectiveLoadProfileCode) =>
        NsmpTimeOfUse(TariffCalcTypes.DayNight, effectiveLoadProfileCode);

    public static MeterPointFacts TimeOfUse(string mcc, string loadProfileCode, decimal? euf24H = null) =>
        new(mcc, loadProfileCode, "DG1", "E", euf24H, null, null);
}
