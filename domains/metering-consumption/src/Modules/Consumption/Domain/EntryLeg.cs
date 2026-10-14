namespace MeteringConsumption.Consumption.Domain;

// One version's half-hours as an entry posts them: as measured on the reading leg, negated on the reversal.
public sealed record EntryLeg(int Rrvn, IReadOnlyList<EntryPeriod> Periods)
{
    public decimal TotalKwh => Periods.Sum(period => period.Kwh);
}
