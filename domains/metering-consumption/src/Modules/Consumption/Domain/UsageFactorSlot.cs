namespace MeteringConsumption.Consumption.Domain;

// Where a usage factor lives in a profile: one estimated and one actual value per timeslot.
public readonly record struct UsageFactorSlot(string Timeslot, UsageFactorKind Kind);
