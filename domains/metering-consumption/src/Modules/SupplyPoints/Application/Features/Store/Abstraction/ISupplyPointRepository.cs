using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;

public interface ISupplyPointRepository
{
    // Null when no message has reached the supply point yet. The supply point stays locked until the unit of work
    // ends, so two units of work on one supply point, such as a redelivery overlapping a run whose session lock expired,
    // are stored one after the other.
    Task<SupplyPoint?> FindAsync(XPRN xprn, CancellationToken cancellationToken);

    Task AddAsync(SupplyPoint supplyPoint, CancellationToken cancellationToken);

    // The key's current rows and the few history rows a statement on the date is decided by, never its whole history.
    Task<KeyFacts<TValues>> FactsAsync<TValues>(ConfigurationKey<TValues> key, DateOnly effectiveDate, CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>;

    // Writes exactly the rows the decision names.
    Task SaveAsync<TValues>(ConfigurationKey<TValues> key, Recorded<TValues> recorded, CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>;
}
