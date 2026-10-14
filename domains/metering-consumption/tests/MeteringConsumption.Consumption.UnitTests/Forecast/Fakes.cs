using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.Consumption.UnitTests.Record;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// The ledger's recent days for one supply point, as the reader would return them, and what it was asked.
internal sealed class FakeLedgerReader : IConsumptionLedgerReader
{
    public List<LedgerDay> Days { get; } = [];

    public List<(string SupplyPointId, DateOnly AsOf, int Days)> Asked { get; } = [];

    public Task<IReadOnlyList<LedgerDay>> ReadRecentDaysAsync(
        string supplyPointId, DateOnly asOf, int days, CancellationToken cancellationToken)
    {
        Asked.Add((supplyPointId, asOf, days));
        return Task.FromResult<IReadOnlyList<LedgerDay>>(Days);
    }
}

// SupplyPoints' answer per supply point, the same whatever the day, and the days it was asked about.
internal sealed class FakeMeterPointConfiguration : IMeterPointConfiguration
{
    public Dictionary<string, MeterPointConfiguration> InEffect { get; } = [];

    public List<DateOnly> AskedDays { get; } = [];

    public Task<MeterPointConfiguration?> InEffectOnAsync(string mprn, DateOnly day, CancellationToken cancellationToken)
    {
        AskedDays.Add(day);
        return Task.FromResult(InEffect.TryGetValue(mprn, out var configuration) ? configuration with { Day = day } : null);
    }
}

internal sealed class FakeTariffReader : ISupplyPointTariffReader
{
    public Dictionary<string, TariffInEffect> Tariffs { get; } = [];

    public Task<TariffInEffect?> FindAsync(string supplyPointId, CancellationToken cancellationToken) =>
        Task.FromResult(Tariffs.GetValueOrDefault(supplyPointId));
}

// Reference data backed by the real V1 load-profile extract, and the area averages a test sets.
internal sealed class FakeReferenceData : IMarketReferenceData
{
    public Dictionary<(string DuosGroup, int Year), AnnualAverageUsage> Averages { get; } = [];

    public Task<LoadProfileCurve> CurveAsync(string loadProfileCode, DateOnly first, DateOnly last, CancellationToken cancellationToken) =>
        Task.FromResult(V1LoadProfiles.Curve(loadProfileCode) ?? new LoadProfileCurve(loadProfileCode, []));

    public Task<AnnualAverageUsage?> AnnualAverageAsync(string duosGroup, int year, CancellationToken cancellationToken) =>
        Task.FromResult(Averages.GetValueOrDefault((duosGroup, year)));
}

// Holds versions the way the forecast table does: the next version per supply point, one current, one per reading.
internal sealed class FakeForecastStore(CallLog log) : IConsumptionForecastStore
{
    public List<StoredForecast> Versions { get; } = [];

    public Task<StoredForecast?> FindByTriggerAsync(
        string supplyPointId, string triggerLedgerEntryId, CancellationToken cancellationToken) =>
        Task.FromResult(Versions.SingleOrDefault(version =>
            version.SupplyPointId == supplyPointId && version.TriggerLedgerEntryId == triggerLedgerEntryId));

    public Task<StoredForecast> SaveAsync(NewForecast forecast, CancellationToken cancellationToken)
    {
        log.Calls.Add("save");
        var mine = Versions.Where(version => version.SupplyPointId == forecast.SupplyPointId).ToArray();
        foreach (var current in mine.Where(version => version.IsCurrent))
        {
            Versions[Versions.IndexOf(current)] = current with { IsCurrent = false };
        }

        var number = mine.Length + 1;
        var stored = new StoredForecast
        {
            SupplyPointId = forecast.SupplyPointId,
            ForecastVersion = number,
            ForecastId = $"{forecast.SupplyPointId}:{number}",
            ComputedAtUtc = forecast.ComputedAtUtc,
            AnchorDate = forecast.AnchorDate,
            HorizonDays = forecast.Daily.Count,
            TotalKwh = forecast.Daily.Sum(day => day.Kwh),
            UsageFactorSource = forecast.UsageFactorSource,
            TriggerLedgerEntryId = forecast.TriggerLedgerEntryId,
            IsCurrent = true,
            Daily = forecast.Daily,
            Inputs = forecast.Inputs,
        };
        Versions.Add(stored);
        return Task.FromResult(stored);
    }

    public Task<int> DeleteSupersededAsync(DateTimeOffset computedBefore, int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
