namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>One day of a consumption forecast: the date and the kWh expected to be used on it.</summary>
public sealed record ForecastDay(DateOnly Date, decimal Kwh);
