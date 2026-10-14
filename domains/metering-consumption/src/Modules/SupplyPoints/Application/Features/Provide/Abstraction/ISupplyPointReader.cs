using MeteringConsumption.SupplyPoints.Application.Features.Provide.DTO;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;

public interface ISupplyPointReader
{
    // Null when no message has reached the supply point. Reads without locking: a read never waits on a Store write.
    Task<SupplyPointOnDay?> ReadAsync(XPRN xprn, DateOnly day, CancellationToken cancellationToken);
}
