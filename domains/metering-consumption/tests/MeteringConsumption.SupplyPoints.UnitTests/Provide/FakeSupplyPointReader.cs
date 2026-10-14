using MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.DTO;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.UnitTests.Provide;

// Holds each supply point's keys as the tables would, and hands back for a day what the reader's queries would: the
// current rows and the history rows on or before it.
internal sealed class FakeSupplyPointReader : ISupplyPointReader
{
    private readonly Dictionary<string, KeyRows<MeterPointValues>> _meterPoints = [];
    private readonly Dictionary<string, Dictionary<string, KeyRows<UsageFactorValues>>> _usageFactors = [];

    public KeyRows<MeterPointValues> MeterPoint(string xprn)
    {
        if (!_meterPoints.TryGetValue(xprn, out var rows))
        {
            rows = new KeyRows<MeterPointValues>();
            _meterPoints.Add(xprn, rows);
            _usageFactors.Add(xprn, []);
        }

        return rows;
    }

    public KeyRows<UsageFactorValues> UsageFactor(string xprn, string timeslotCode)
    {
        MeterPoint(xprn);
        if (!_usageFactors[xprn].TryGetValue(timeslotCode, out var rows))
        {
            rows = new KeyRows<UsageFactorValues>();
            _usageFactors[xprn].Add(timeslotCode, rows);
        }

        return rows;
    }

    public Task<SupplyPointOnDay?> ReadAsync(XPRN xprn, DateOnly day, CancellationToken cancellationToken) =>
        Task.FromResult(_meterPoints.TryGetValue(xprn.Value, out var meterPoint)
            ? new SupplyPointOnDay(OnDay(meterPoint, day),
                _usageFactors[xprn.Value].ToDictionary(timeslot => timeslot.Key, timeslot => OnDay(timeslot.Value, day)))
            : null);

    private static KeyOnDay<TValues> OnDay<TValues>(KeyRows<TValues> rows, DateOnly day)
        where TValues : class, ICarriedValues<TValues> =>
        new([.. rows.Current.Where(row => row.EffectiveDate <= day)], [.. rows.History.Where(row => row.EffectiveDate <= day)]);
}
