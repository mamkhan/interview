namespace MeteringConsumption.Consumption.Domain;

// The forecast's tunable values. The own-use window runs from PufMaxDays − 1 to PufMinDays before the anchor; an
// estimated usage factor counts only strictly inside the acceptable range, in kWh a year.
public sealed record ForecastSettings(
    int HorizonDays,
    int PufMinDays,
    int PufMaxDays,
    decimal EufMinAcceptableKwh,
    decimal EufMaxAcceptableKwh);
