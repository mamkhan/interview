using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

// A forecast version as stored: each day's kWh to six decimals, and the total as the sum of those days. A superseded
// version's inputs are empty.
public sealed record StoredForecast
{
    public required string SupplyPointId { get; init; }

    public required long ForecastVersion { get; init; }

    public required string ForecastId { get; init; }

    public required DateTimeOffset ComputedAtUtc { get; init; }

    public required DateOnly AnchorDate { get; init; }

    public required int HorizonDays { get; init; }

    public required decimal TotalKwh { get; init; }

    public required string UsageFactorSource { get; init; }

    public required string TriggerLedgerEntryId { get; init; }

    public required bool IsCurrent { get; init; }

    public required IReadOnlyList<ForecastedDay> Daily { get; init; }

    public required string Inputs { get; init; }
}
