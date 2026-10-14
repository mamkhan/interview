using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// Holds each supply point's profile the way the table does: a save writes one group of figures and keeps the
// others as stored, and a find returns a copy, so a handler that applies without saving changes nothing here.
internal sealed class FakeProfileRepository : ISupplyPointProfileRepository
{
    private readonly Dictionary<string, SupplyPointProfile> _profiles = [];

    public List<string> Saves { get; } = [];

    public DateTimeOffset? UpdatedAt { get; private set; }

    public SupplyPointProfile? Stored(string supplyPointId) => _profiles.GetValueOrDefault(supplyPointId);

    public void Seed(SupplyPointProfile profile) => _profiles[profile.SupplyPointId] = Copy(profile);

    public Task<SupplyPointProfile?> FindAsync(string supplyPointId, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.TryGetValue(supplyPointId, out var stored) ? Copy(stored) : null);

    public Task SaveConfigurationAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
        Save(profile, updatedAt, "configuration", (saved, stored) => Restore(saved, configuration: saved.Configuration, from: stored));

    public Task SaveUsageFactorsAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
        Save(profile, updatedAt, "usage factors", (saved, stored) => Restore(saved, usageFactors: saved.UsageFactors.Values, from: stored));

    public Task SaveTariffAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
        Save(profile, updatedAt, "tariff", (saved, stored) => Restore(saved, tariff: saved.Tariff, from: stored));

    public Task SaveSmartMeterFlagAsync(SupplyPointProfile profile, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
        Save(profile, updatedAt, "smart meter flag", (saved, stored) => Restore(saved, smartMeterFlag: saved.SmartMeterFlag, from: stored));

    private Task Save(SupplyPointProfile profile, DateTimeOffset updatedAt, string group,
        Func<SupplyPointProfile, SupplyPointProfile?, SupplyPointProfile> write)
    {
        _profiles[profile.SupplyPointId] = write(profile, _profiles.GetValueOrDefault(profile.SupplyPointId));
        Saves.Add(group);
        UpdatedAt = updatedAt;
        return Task.CompletedTask;
    }

    // The saved group comes from the profile being saved; every other group stays as stored.
    private static SupplyPointProfile Restore(
        SupplyPointProfile saved,
        SupplyPointProfile? from,
        MeterConfigurationStatement? configuration = null,
        IEnumerable<UsageFactorStatement>? usageFactors = null,
        TariffStatement? tariff = null,
        SmartMeterFlagStatement? smartMeterFlag = null) =>
        SupplyPointProfile.Restore(
            saved.SupplyPointId,
            saved.Market,
            configuration ?? from?.Configuration,
            usageFactors ?? from?.UsageFactors.Values ?? [],
            tariff ?? from?.Tariff,
            smartMeterFlag ?? from?.SmartMeterFlag);

    private static SupplyPointProfile Copy(SupplyPointProfile profile) =>
        SupplyPointProfile.Restore(profile.SupplyPointId, profile.Market, profile.Configuration, profile.UsageFactors.Values,
            profile.Tariff, profile.SmartMeterFlag);
}
