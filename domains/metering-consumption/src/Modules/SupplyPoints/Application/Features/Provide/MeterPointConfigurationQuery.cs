using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Provide;

// The same read for today, a future day or any past day: each key answered from current when it reaches back to the
// day, and from history otherwise.
public sealed class MeterPointConfigurationQuery(ISupplyPointReader reader) : IMeterPointConfiguration
{
    public async Task<MeterPointConfiguration?> InEffectOnAsync(string mprn, DateOnly day, CancellationToken cancellationToken)
    {
        var supplyPoint = await reader.ReadAsync(new XPRN(mprn), day, cancellationToken);
        if (supplyPoint is null)
        {
            return null;
        }

        var meterPoint = Recording.InEffectOn(day, supplyPoint.MeterPoint.Current, supplyPoint.MeterPoint.StatedOnOrBefore);
        var usageFactors = new List<UsageFactorInEffect>();
        foreach (var (timeslotCode, rows) in supplyPoint.UsageFactors.OrderBy(factor => factor.Key, StringComparer.Ordinal))
        {
            if (Recording.InEffectOn(day, rows.Current, rows.StatedOnOrBefore) is { } values)
            {
                usageFactors.Add(new UsageFactorInEffect(timeslotCode, values.Estimated, values.Actual));
            }
        }

        if (meterPoint is null && usageFactors.Count == 0)
        {
            return null;
        }

        return new MeterPointConfiguration
        {
            MPRN = mprn,
            Day = day,
            MeterConfigurationCode = meterPoint?.MeterConfigurationCode,
            LoadProfileCode = meterPoint?.LoadProfileCode,
            DuosGroup = meterPoint?.DuosGroup,
            MeterPointStatusCode = meterPoint?.MeterPointStatusCode,
            UsageFactors = usageFactors,
        };
    }
}
