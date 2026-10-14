namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

public sealed record CurrentWrite<TValues>(CurrentWriteKind Kind, Dated<TValues> Row);
