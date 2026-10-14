using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;

public interface IConsumptionForecastStore
{
    Task<StoredForecast?> FindByTriggerAsync(string supplyPointId, string triggerLedgerEntryId, CancellationToken cancellationToken);

    // Stores the forecast as the supply point's next version and its current one, and returns it as stored.
    Task<StoredForecast> SaveAsync(NewForecast forecast, CancellationToken cancellationToken);

    // Deletes up to batchSize superseded versions computed before the given time; returns how many it deleted.
    Task<int> DeleteSupersededAsync(DateTimeOffset computedBefore, int batchSize, CancellationToken cancellationToken);
}
