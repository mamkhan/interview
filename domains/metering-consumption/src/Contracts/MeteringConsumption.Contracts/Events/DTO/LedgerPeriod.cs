namespace MeteringConsumption.Contracts.Events.DTO;

/// <summary>One half-hour of a ledger leg: its ordinal within the read date, its energy in kWh and the
/// operator's status code for it.</summary>
public sealed record LedgerPeriod(int Period, decimal Kwh, string Status);
