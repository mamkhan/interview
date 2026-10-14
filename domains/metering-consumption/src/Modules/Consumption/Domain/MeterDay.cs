namespace MeteringConsumption.Consumption.Domain;

// The ledger's account: one supply point's consumption on one read date. The meter serial is not part of
// it, so an exchanged meter keeps one series for the supply point.
public sealed record MeterDay
{
    public MeterDay(string supplyPointId, DateOnly readDate)
    {
        if (string.IsNullOrWhiteSpace(supplyPointId))
        {
            throw new LedgerEntryRejectedException("A reading names no supply point.");
        }

        SupplyPointId = supplyPointId;
        ReadDate = readDate;
    }

    public string SupplyPointId { get; }

    public DateOnly ReadDate { get; }
}
