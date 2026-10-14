namespace MeteringConsumption.Contracts.Events.DTO;

public sealed record UsageFactor(string TimeslotCode, DateOnly EffectiveFromDate, decimal? ActualUsageFactor, decimal? EstimatedUsageFactor);
