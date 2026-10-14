using BuildingBlocks.Seedwork;

namespace MeteringConsumption.Consumption.Domain;

// Input the forecast cannot be built from at all, such as a window with no read day: deterministic, so the
// message is dead-lettered for someone to look at.
public sealed class ForecastInputRejectedException(string message) : DomainException(message);
