using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;

public interface ISupplyPointReloadRepository
{
    // Null when nothing has reached the supply point yet. The supply point stays locked until the unit of work ends, so
    // a reload and a market message for one supply point are stored one after the other.
    Task<SupplyPoint?> FindAsync(XPRN xprn, CancellationToken cancellationToken);

    Task AddAsync(SupplyPoint supplyPoint, CancellationToken cancellationToken);

    // Deletes every meter point and usage factor row of the supply point, history and current, and writes the outcome's
    // rows in their place.
    Task SaveReloadAsync(XPRN xprn, ReloadOutcome outcome, CancellationToken cancellationToken);
}
