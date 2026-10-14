using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// What one statement is about at a supply point; each kind of key has its own values and its own pair of tables.
public abstract record ConfigurationKey<TValues>(XPRN Xprn)
    where TValues : class, ICarriedValues<TValues>;

public sealed record MeterPointKey(XPRN Xprn) : ConfigurationKey<MeterPointValues>(Xprn);

public sealed record UsageFactorKey(XPRN Xprn, string TimeslotCode) : ConfigurationKey<UsageFactorValues>(Xprn);

public sealed record MeterKey(XPRN Xprn, string SerialNumber) : ConfigurationKey<MeterDetails>(Xprn);

public sealed record RegisterKey(XPRN Xprn, RegisterId Register) : ConfigurationKey<RegisterValues>(Xprn);
