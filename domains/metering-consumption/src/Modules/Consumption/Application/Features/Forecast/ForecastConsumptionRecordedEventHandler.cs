using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Queries;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Application.Features.Forecast;

// The session on the supply point gives this handler one recorded reading per supply point at a time, in order. A
// forecast is stored before it is announced, and a reading whose forecast is already stored is announced again under
// the same id while it is current, so a crash between the two is repaired by the redelivery. A superseded one was
// announced before the reading that superseded it was handled.
public sealed class ForecastConsumptionRecordedEventHandler(
    IConsumptionLedgerReader ledger,
    IMeterPointConfiguration configurations,
    ISupplyPointTariffReader tariffs,
    IMarketReferenceData referenceData,
    IConsumptionForecastStore store,
    IEventPublisher publisher,
    ForecastSettings settings,
    TimeProvider time,
    ILogger<ForecastConsumptionRecordedEventHandler> logger) : IEventHandler<ConsumptionRecorded>
{
    // Names, not numbers, for the layout and the usage factor source, so a stored forecast reads on its own.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task HandleAsync(ConsumptionRecorded message, CancellationToken cancellationToken)
    {
        var supplyPointId = message.SupplyPointId;
        if (await store.FindByTriggerAsync(supplyPointId, message.LedgerEntryId, cancellationToken) is { } stored)
        {
            if (stored.IsCurrent)
            {
                await AnnounceAsync(stored, message.CorrelationId, cancellationToken);
            }

            return;
        }

        var days = await ledger.ReadRecentDaysAsync(
            supplyPointId, DateOnly.FromDateTime(message.PostedAtUtc.UtcDateTime), settings.PufMaxDays - 1, cancellationToken);
        var window = ConsumptionWindow.Of(days.Select(day => day.Day));
        var anchor = window.Anchor;

        var configuration = await configurations.InEffectOnAsync(supplyPointId, anchor, cancellationToken);
        var tariff = await tariffs.FindAsync(supplyPointId, cancellationToken);
        var facts = configuration is null ? null : ToFacts(configuration);
        var curves = await CurvesAsync(ForecastProfile.Resolve(facts, tariff?.Facts), anchor, cancellationToken);
        var annualAverage = configuration?.DuosGroup is { } duosGroup
            ? await referenceData.AnnualAverageAsync(duosGroup, anchor.Year, cancellationToken)
            : null;

        var outcome = ForecastCalculation.Calculate(
            window, facts, tariff?.Facts, code => curves.GetValueOrDefault(code), annualAverage, settings);
        if (outcome is ForecastOutcome.NotForecast notForecast)
        {
            ForecastTelemetry.RecordNotForecast(notForecast.Reason);
            logger.LogWarning("{SupplyPointId}: no forecast from {LedgerEntryId}, {Reason}",
                supplyPointId, message.LedgerEntryId, notForecast.Reason);
            return;
        }

        var result = outcome is ForecastOutcome.Computed computed
            ? computed.Result
            : ((ForecastOutcome.ZeroForecast)outcome).Result;
        var computedAt = time.GetUtcNow();
        var inputs = new ForecastInputs
        {
            ConfigurationDay = anchor,
            Configuration = configuration!,
            Tariff = tariff!,
            Profile = result.Profile,
            AnnualAverageYear = anchor.Year,
            AnnualAverage = annualAverage,
            PufWindow = result.PufWindow is { } puf ? new PufWindowInputs(puf.Baseline, puf.LatestReadDate, puf.Days) : null,
            LedgerDays = [.. days.Select(day => new LedgerDayInput(day.Day.ReadDate, day.Rrvn))],
            Factors = UsageFactorInputs.From(result.Factors, result.Profile.Layout),
            Settings = settings,
        };

        var saved = await store.SaveAsync(new NewForecast
        {
            SupplyPointId = supplyPointId,
            ComputedAtUtc = computedAt,
            AnchorDate = result.AnchorDate,
            Daily = result.Days,
            UsageFactorSource = SourceName(result.Factors.Source),
            TriggerLedgerEntryId = message.LedgerEntryId,
            Inputs = JsonSerializer.Serialize(inputs, Json),
        }, cancellationToken);
        await AnnounceAsync(saved, message.CorrelationId, cancellationToken);

        var lag = computedAt - message.PostedAtUtc;
        if (outcome is ForecastOutcome.Computed)
        {
            ForecastTelemetry.RecordComputed(lag);
        }
        else
        {
            ForecastTelemetry.RecordZeroForecast(lag);
            logger.LogWarning("{SupplyPointId}: no usable usage factor, so {ForecastId} is a forecast of zeros",
                supplyPointId, saved.ForecastId);
        }
    }

    // The 24H, 00D and 00N timeslots' estimated factors are the EUF; the actual factors are Estimate's.
    private static MeterPointFacts ToFacts(MeterPointConfiguration configuration)
    {
        decimal? Estimated(string timeslot) => configuration.UsageFactors
            .FirstOrDefault(factor => factor.TimeslotCode == timeslot)?.EstimatedUsageFactor;

        return new MeterPointFacts(
            configuration.MeterConfigurationCode,
            configuration.LoadProfileCode,
            configuration.DuosGroup,
            configuration.MeterPointStatusCode,
            Estimated("24H"),
            Estimated("00D"),
            Estimated("00N"));
    }

    // The own-use window and the horizon, for the meter's own profile and the profile the forecast is shaped by.
    private async Task<Dictionary<string, LoadProfileCurve>> CurvesAsync(
        ProfileResolution resolution, DateOnly anchor, CancellationToken cancellationToken)
    {
        var curves = new Dictionary<string, LoadProfileCurve>();
        if (resolution is not ProfileResolution.Resolved { Profile: var profile })
        {
            return curves;
        }

        var first = anchor.AddDays(-(settings.PufMaxDays - 1));
        var last = anchor.AddDays(settings.HorizonDays - 1);
        foreach (var code in new[] { profile.OwnLoadProfile, profile.CurveLoadProfile }.Distinct())
        {
            curves[code] = await referenceData.CurveAsync(code, first, last, cancellationToken);
        }

        return curves;
    }

    private static string SourceName(UsageFactorSource source) => source switch
    {
        UsageFactorSource.Puf => "PUF",
        UsageFactorSource.Aau => "AAU",
        UsageFactorSource.Euf => "EUF",
        _ => "None",
    };

    // The event is built from the stored row alone, so announcing it again yields the same id, time and values.
    private Task AnnounceAsync(StoredForecast stored, string correlationId, CancellationToken cancellationToken) =>
        publisher.PublishAsync(new ConsumptionForecastUpdated
        {
            EventId = DeterministicGuid.From(stored.ForecastId),
            PublishingContext = ConsumptionTopology.PublishingContext,
            OccurredAtUtc = stored.ComputedAtUtc,
            CorrelationId = correlationId,
            SupplyPointId = stored.SupplyPointId,
            ForecastId = stored.ForecastId,
            ForecastVersion = stored.ForecastVersion,
            AnchorDate = stored.AnchorDate,
            HorizonDays = stored.HorizonDays,
            TotalKwh = stored.TotalKwh,
            UsageFactorSource = stored.UsageFactorSource,
            Daily = [.. stored.Daily.Select(day => new ForecastDay(day.Date, day.Kwh))],
            TriggerLedgerEntryId = stored.TriggerLedgerEntryId,
            ComputedAtUtc = stored.ComputedAtUtc,
        }, new EventMetadata
        {
            MessageId = stored.ForecastId,
            CorrelationId = correlationId,
            SessionId = stored.SupplyPointId,
        }, cancellationToken);
}
