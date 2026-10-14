namespace MeteringConsumption.Consumption.UnitTests.Domain;

// Three real production NSMP time-of-use meters on business day 2026-07-28, from V1's diagnostics dumps: each tier's
// cumulative register at the excluded window baseline and at the latest read, 2026-07-27, as V1 held them. The
// window's own use per tier is the difference of the two, which is all a forecast reads of the days between.
public static class V1TimeOfUseMeters
{
    public static readonly DateOnly LatestReadDate = new(2026, 7, 27);

    // 10900000901: MCC16, ESB profile 25, tariff 24-hour on LP1; window 2026-06-30 to 07-27.
    public static readonly DateOnly SmartFlatBaseline = new(2026, 6, 29);

    // 10900000902: MCC16, ESB profile 25, tariff day/night on LP2; same window.
    public static readonly DateOnly SmartBandedBaseline = new(2026, 6, 29);

    // 10900000903: MCC12, ESB profile 02, tariff day/night on LP2; window 2026-07-01 to 07-27.
    public static readonly DateOnly DayNightOnlyBaseline = new(2026, 6, 30);

    public static readonly decimal[] SmartFlatBaselineRegisters =
    [
        0.765m, 0.718m, 0.678m, 0.645m, 0.647m, 0.644m,
        0.666m, 0.685m, 0.692m, 0.699m, 0.713m, 0.715m,
        0.771m, 0.803m, 0.843m, 0.877m, 1.431m, 1.411m,
        1.42m, 1.4m, 1.388m, 1.371m, 1.381m, 1.36m,
        1.378m, 1.384m, 1.392m, 1.33m, 1.306m, 1.294m,
        1.292m, 1.3m, 1.33m, 1.388m, 2.419m, 2.553m,
        2.708m, 2.731m, 1.688m, 1.626m, 1.591m, 1.6m,
        1.588m, 1.522m, 1.472m, 1.397m, 0.828m, 0.773m,
    ];

    public static readonly decimal[] SmartFlatLatestRegisters =
    [
        1.685m, 1.527m, 1.418m, 1.332m, 1.302m, 1.275m,
        1.3m, 1.316m, 1.329m, 1.349m, 1.377m, 1.387m,
        1.549m, 1.698m, 1.873m, 1.94m, 2.677m, 2.73m,
        2.811m, 2.849m, 2.86m, 2.876m, 2.855m, 2.839m,
        2.871m, 2.893m, 2.905m, 2.779m, 2.694m, 2.648m,
        2.657m, 2.692m, 2.776m, 2.923m, 4.366m, 4.625m,
        4.865m, 4.874m, 3.449m, 3.302m, 3.215m, 3.221m,
        3.179m, 3.049m, 3.002m, 2.849m, 2.013m, 1.826m,
    ];

    public static readonly decimal[] SmartBandedBaselineRegisters =
    [
        4.835m, 4.201m, 3.678m, 3.382m, 3.17m, 3.013m,
        2.858m, 2.817m, 2.88m, 2.878m, 2.96m, 3.091m,
        3.798m, 4.716m, 5.608m, 5.811m, 6.193m, 6.708m,
        6.923m, 7.0m, 7.149m, 7.146m, 7.315m, 7.278m,
        7.548m, 7.653m, 7.625m, 7.257m, 7.112m, 6.925m,
        6.89m, 7.1m, 7.353m, 7.915m, 8.471m, 9.101m,
        9.42m, 9.463m, 9.059m, 8.61m, 8.577m, 8.436m,
        8.33m, 8.141m, 7.896m, 7.393m, 6.586m, 5.615m,
    ];

    public static readonly decimal[] SmartBandedLatestRegisters =
    [
        8.913m, 7.738m, 6.873m, 6.371m, 5.95m, 5.649m,
        5.475m, 5.346m, 5.415m, 5.487m, 5.658m, 5.827m,
        7.096m, 8.714m, 10.472m, 10.786m, 11.341m, 12.115m,
        12.601m, 12.896m, 13.197m, 13.342m, 13.433m, 13.414m,
        13.72m, 13.978m, 14.045m, 13.271m, 12.846m, 12.495m,
        12.542m, 12.998m, 13.572m, 14.624m, 15.694m, 17.028m,
        17.577m, 17.506m, 16.649m, 15.774m, 15.577m, 15.422m,
        15.238m, 14.869m, 14.601m, 13.663m, 12.171m, 10.444m,
    ];

    public static readonly decimal[] DayNightOnlyBaselineRegisters =
    [
        0.233m, 0.216m, 0.206m, 0.197m, 0.207m, 0.202m,
        0.213m, 0.228m, 0.236m, 0.238m, 0.249m, 0.245m,
        0.26m, 0.266m, 0.263m, 0.28m, 0.282m, 0.271m,
        0.217m, 0.212m, 0.209m, 0.203m, 0.204m, 0.208m,
        0.206m, 0.209m, 0.206m, 0.198m, 0.197m, 0.193m,
        0.193m, 0.201m, 0.198m, 0.205m, 0.225m, 0.237m,
        0.254m, 0.255m, 0.25m, 0.247m, 0.238m, 0.238m,
        0.224m, 0.215m, 0.216m, 0.202m, 0.189m, 0.18m,
    ];

    public static readonly decimal[] DayNightOnlyLatestRegisters =
    [
        1.863m, 1.505m, 1.46m, 1.419m, 1.415m, 1.391m,
        1.402m, 1.48m, 1.551m, 1.602m, 1.553m, 1.572m,
        1.658m, 1.624m, 1.64m, 3.77m, 4.666m, 4.692m,
        3.807m, 3.069m, 2.145m, 1.549m, 1.288m, 1.199m,
        1.098m, 1.054m, 1.104m, 1.111m, 1.075m, 1.003m,
        1.0m, 1.067m, 1.057m, 1.058m, 1.062m, 1.196m,
        1.202m, 1.214m, 1.206m, 1.128m, 1.228m, 1.184m,
        1.514m, 1.125m, 1.085m, 1.141m, 1.522m, 1.397m,
    ];
}
