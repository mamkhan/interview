namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>One version's half-hours as the ledger posts them: as measured on a reading leg, negated on a
/// reversal leg.</summary>
public sealed record LedgerLeg(int RRVN, IReadOnlyList<LedgerPeriod> Periods)
{
    public decimal TotalKwh => Periods.Sum(period => period.Kwh);
}
