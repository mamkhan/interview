using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;

public interface ISupplyPointTariffReader
{
    // Null when the Profile holds no row for the supply point.
    Task<TariffInEffect?> FindAsync(string supplyPointId, CancellationToken cancellationToken);
}
