namespace MeteringConsumption.Consumption.Domain;

// Market Messaging passes interval values through as the operator states them; the ledger is in kWh. KWT is
// the average power over the interval in kW, so its energy is the value times the interval in hours. Any other
// unit is refused rather than guessed at: a wrong unit would post every half-hour at the wrong size.
public static class IntervalEnergy
{
    public const string AveragePowerKw = "KWT";

    public static decimal ToKwh(decimal value, string uomCode, int intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new LedgerEntryRejectedException($"An interval of {intervalMinutes} minutes has no length.");
        }

        return uomCode == AveragePowerKw
            ? value * intervalMinutes / 60m
            : throw new LedgerEntryRejectedException($"Unit {uomCode} is not one the ledger converts to kWh.");
    }
}
