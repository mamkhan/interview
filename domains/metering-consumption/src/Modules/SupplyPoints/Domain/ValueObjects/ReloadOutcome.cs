using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// The rows that replace every meter point and usage factor row a supply point held, history and current, so the
// repository writes exactly these.
public sealed record ReloadOutcome(KeyRows<MeterPointValues> MeterPoint, IReadOnlyDictionary<string, KeyRows<UsageFactorValues>> UsageFactors);
