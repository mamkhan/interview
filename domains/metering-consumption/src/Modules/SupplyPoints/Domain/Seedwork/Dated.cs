namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

// One key's values from a date: what a statement stated, a history row, or a current row.
public sealed record Dated<TValues>(DateOnly EffectiveDate, TValues Values);
