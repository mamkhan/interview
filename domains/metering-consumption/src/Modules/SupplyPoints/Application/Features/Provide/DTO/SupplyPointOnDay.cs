using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Application.Features.Provide.DTO;

// What one supply point's tables hold for a day: its meter point and each timeslot it has usage factors for.
public sealed record SupplyPointOnDay(KeyOnDay<MeterPointValues> MeterPoint, IReadOnlyDictionary<string, KeyOnDay<UsageFactorValues>> UsageFactors);

// One key's current rows on or before the day, and the history rows that give each value its latest statement on or
// before it.
public sealed record KeyOnDay<TValues>(IReadOnlyList<Dated<TValues>> Current, IReadOnlyList<Dated<TValues>> StatedOnOrBefore);
