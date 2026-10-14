namespace MeteringConsumption.Contracts.Events.DTO;

public sealed record IntervalPeriod(DateTimeOffset Timestamp, decimal Value, string StatusCode);
