using BuildingBlocks.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.Exceptions;

public sealed class SupplyPointRejectedException(string message) : DomainException(message);
