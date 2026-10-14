using MeteringConsumption.Consumption.Application.Features.Forecast;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.UnitTests.Record;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.UnitTests.Forecast;

// The Forecast handler over fakes for its sources, for the single-rate persona 10001234567: MCC1 on ESB profile 01 in
// DG1, not time-of-use, read at 12 kWh a day up to 2026-04-30, the reading posted on 2026-05-01.
internal sealed class ForecastHandling
{
    public const string SupplyPointId = "10001234567";

    public static readonly DateTimeOffset PostedAt = new(2026, 5, 1, 2, 14, 0, 417, TimeSpan.Zero);

    public static readonly DateTimeOffset Now = new(2026, 5, 1, 2, 14, 3, 125, TimeSpan.Zero);

    public ForecastHandling()
    {
        Publisher = new RecordingPublisher(Log);
        Store = new FakeForecastStore(Log);
        Configurations.InEffect[SupplyPointId] = new MeterPointConfiguration
        {
            MPRN = SupplyPointId,
            Day = new DateOnly(2026, 5, 1),
            MeterConfigurationCode = "MCC01",
            LoadProfileCode = "01",
            DuosGroup = "DG1",
            MeterPointStatusCode = "E",
            UsageFactors = [new UsageFactorInEffect("24H", null, 3100m)],
        };
        Tariffs.Tariffs[SupplyPointId] = new TariffInEffect(
            ForecastPersonas.NotTimeOfUse, new DateTimeOffset(2026, 4, 30, 21, 0, 0, TimeSpan.Zero), "v1-reader",
            new DateTimeOffset(2026, 4, 30, 21, 0, 0, TimeSpan.Zero), "v1-reader");
        ReferenceData.Averages[("DG1", 2026)] = ForecastPersonas.Dg1In2026;
        WithReadDays(10);
    }

    public CallLog Log { get; } = new();

    public FakeLedgerReader Ledger { get; } = new();

    public FakeMeterPointConfiguration Configurations { get; } = new();

    public FakeTariffReader Tariffs { get; } = new();

    public FakeReferenceData ReferenceData { get; } = new();

    public FakeForecastStore Store { get; }

    public RecordingPublisher Publisher { get; }

    public RecordingLogger<ForecastConsumptionRecordedEventHandler> Logger { get; } = new();

    public IEnumerable<ConsumptionForecastUpdated> Announced =>
        Publisher.Published.Select(published => (ConsumptionForecastUpdated)published.Event);

    public void WithReadDays(int days)
    {
        Ledger.Days.Clear();
        var latest = new DateOnly(2026, 4, 30);
        for (var day = days - 1; day >= 0; day--)
        {
            var slots = new decimal[ReadDay.SlotsPerDay];
            slots[0] = 12m;
            var readDate = latest.AddDays(-day);
            Ledger.Days.Add(new LedgerDay(ReadDay.Of(readDate, slots), $"hash:{SupplyPointId}:{readDate:yyyy-MM-dd}:1", 1));
        }
    }

    public static ConsumptionRecorded Recorded(string ledgerEntryId = $"hash:{SupplyPointId}:2026-04-30:1") => new()
    {
        EventId = Guid.Parse("3ae8c6ab-82fe-fc3d-3b08-9677737a0a19"),
        PublishingContext = "metering-consumption.consumption",
        OccurredAtUtc = PostedAt,
        CorrelationId = "receipt-1",
        LedgerEntryId = ledgerEntryId,
        SupplyPointId = SupplyPointId,
        MeterSerial = "M123456",
        ReadDate = new DateOnly(2026, 4, 30),
        RRVN = 1,
        EntryType = "ReadingReceived",
        PostedAtUtc = PostedAt,
        MeteringIntervalMinutes = 30,
        UOMCode = "KWT",
        Fuel = "electricity",
        Reading = new LedgerLeg(1, [new LedgerPeriod(1, 12m, "VVAK")]),
        Provenance = RecordedEntryBuilder.Provenance(),
    };

    public Task DeliverAsync(ConsumptionRecorded recorded) =>
        new ForecastConsumptionRecordedEventHandler(
                Ledger, Configurations, Tariffs, ReferenceData, Store, Publisher, ForecastPersonas.Settings,
                new FixedTimeProvider(Now), Logger)
            .HandleAsync(recorded, CancellationToken.None);
}
