using BuildingBlocks.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Domain.Aggregates;

// What ESB states about one supply point. Its row is the lock that orders every write for it; what it holds per key is
// read and written a key at a time.
public sealed class SupplyPoint : AggregateRoot<XPRN>
{
    private SupplyPoint(XPRN xprn, string market) : base(xprn)
    {
        Market = market;
    }

    public string Market { get; }

    public static SupplyPoint New(XPRN xprn, string market) => new(xprn, market);

    public static SupplyPoint Restore(XPRN xprn, string market) => new(xprn, market);

    // A reload replaces everything known about the meter point and the usage factors, whatever stated it, so its
    // statements are recorded on empty keys, in date order as messages arriving over time would have been. The meters and
    // registers stay as they were.
    public static ReloadOutcome Reload(Dated<MeterPointValues> meterPoint,
        IEnumerable<(string TimeslotCode, Dated<UsageFactorValues> Statement)> usageFactors, DateOnly today)
    {
        var meterPointRows = new KeyRows<MeterPointValues>();
        meterPointRows.Record(meterPoint, today);

        var usageFactorRows = new Dictionary<string, KeyRows<UsageFactorValues>>();
        foreach (var (timeslotCode, statement) in usageFactors.OrderBy(factor => factor.Statement.EffectiveDate))
        {
            if (!usageFactorRows.TryGetValue(timeslotCode, out var rows))
            {
                rows = new KeyRows<UsageFactorValues>();
                usageFactorRows.Add(timeslotCode, rows);
            }

            rows.Record(statement, today);
        }

        return new ReloadOutcome(meterPointRows, usageFactorRows);
    }
}
