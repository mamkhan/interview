using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;

public interface ISupplyPointProfileRepository
{
    // Null when no message has reached the supply point yet.
    Task<SupplyPointProfile?> FindAsync(string supplyPointId, CancellationToken cancellationToken);

    // Writes the configuration columns only, creating the row when the supply point has none yet.
    Task SaveConfigurationAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    // Writes the usage factor columns only, creating the row when the supply point has none yet.
    Task SaveUsageFactorsAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    // Writes the tariff columns only, creating the row when the supply point has none yet.
    Task SaveTariffAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    // Writes the smart meter flag columns only, creating the row when the supply point has none yet.
    Task SaveSmartMeterFlagAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
