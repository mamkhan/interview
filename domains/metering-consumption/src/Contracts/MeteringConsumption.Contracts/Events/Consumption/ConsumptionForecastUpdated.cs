using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.Consumption;

/// <summary>A supply point's new consumption forecast, one value per day from the anchor date, and the recorded reading
/// that triggered it. <c>UsageFactorSource</c> is <c>PUF</c>, <c>AAU</c>, <c>EUF</c>, or <c>None</c> for a forecast of
/// zeros. Published with the forecast id as message id and the supply point as session id.</summary>
public sealed record ConsumptionForecastUpdated : EventEnvelope
{
    public required string SupplyPointId { get; init; }

    public required string ForecastId { get; init; }

    public required long ForecastVersion { get; init; }

    public required DateOnly AnchorDate { get; init; }

    public required int HorizonDays { get; init; }

    public required decimal TotalKwh { get; init; }

    public required string UsageFactorSource { get; init; }

    public required IReadOnlyList<ForecastDay> Daily { get; init; }

    public required string TriggerLedgerEntryId { get; init; }

    public required DateTimeOffset ComputedAtUtc { get; init; }
}
