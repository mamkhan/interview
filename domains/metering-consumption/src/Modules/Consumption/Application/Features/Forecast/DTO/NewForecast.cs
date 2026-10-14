using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

// A forecast to store as the supply point's next version. Inputs is the JSON record of everything it was made from.
public sealed record NewForecast
{
    public required string SupplyPointId { get; init; }

    public required DateTimeOffset ComputedAtUtc { get; init; }

    public required DateOnly AnchorDate { get; init; }

    public required IReadOnlyList<ForecastedDay> Daily { get; init; }

    public required string UsageFactorSource { get; init; }

    public required string TriggerLedgerEntryId { get; init; }

    public required string Inputs { get; init; }
}
