using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Queries;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

// Everything a stored forecast was made from, written once beside it so the forecast can be explained later. Nothing
// derivable is repeated: a ledger day is its date and version, which name its file, and the factors are kept per band.
public sealed record ForecastInputs
{
    public required DateOnly ConfigurationDay { get; init; }

    public required MeterPointConfiguration Configuration { get; init; }

    public required TariffInEffect Tariff { get; init; }

    public required ForecastProfile Profile { get; init; }

    public required int AnnualAverageYear { get; init; }

    public AnnualAverageUsage? AnnualAverage { get; init; }

    public PufWindowInputs? PufWindow { get; init; }

    public required IReadOnlyList<LedgerDayInput> LedgerDays { get; init; }

    public required UsageFactorInputs Factors { get; init; }

    public required ForecastSettings Settings { get; init; }
}

public sealed record PufWindowInputs(DateOnly Baseline, DateOnly LatestReadDate, int Days);

public sealed record LedgerDayInput(DateOnly ReadDate, int Rrvn);

// An own-use factor keeps the kWh measured per tier and the load-profile sum per band, the factor being one over the
// other; any other factor keeps its annual kWh per band. A band list is one value for a flat layout and night then day
// for a banded one.
public sealed record UsageFactorInputs(
    UsageFactorSource Source,
    IReadOnlyList<decimal>? PerBand,
    IReadOnlyList<decimal>? Numerators,
    IReadOnlyList<decimal>? DenominatorsPerBand)
{
    public static UsageFactorInputs From(UsageFactors factors, CurveLayout layout) => factors.Source == UsageFactorSource.Puf
        ? new(factors.Source, null, factors.Numerators, Banded(factors.Denominators!, layout))
        : new(factors.Source, Banded(factors.PerTier, layout), null, null);

    private static decimal[] Banded(IReadOnlyList<decimal> perTier, CurveLayout layout) => layout switch
    {
        CurveLayout.BandedTimeOfUse => [perTier[0], perTier[16]],
        CurveLayout.FlatTimeOfUse => [perTier[0]],
        _ => [.. perTier],
    };
}
