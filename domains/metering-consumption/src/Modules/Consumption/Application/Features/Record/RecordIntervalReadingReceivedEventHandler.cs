using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using MeteringConsumption.Consumption.Application.Features.Record.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Application.Features.Record;

// The session on the supply point gives this handler one reading per supply point at a time, so loading the
// meter-day and appending to it cannot race. The kept entry is the only record that a version was seen: it is
// written before it is announced, and a version found already kept is announced again under the same id, so a
// crash between the two is repaired by the redelivery rather than losing the announcement.
public sealed class RecordIntervalReadingReceivedEventHandler(
    IConsumptionLedgerStore store,
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<RecordIntervalReadingReceivedEventHandler> logger) : IEventHandler<IntervalReadingReceived>
{
    public async Task HandleAsync(IntervalReadingReceived message, CancellationToken cancellationToken)
    {
        if (MeteringConsumptionTopology.Markets.FuelOf(message.Provenance.Market) is null)
        {
            throw new LedgerEntryRejectedException($"Market {message.Provenance.Market} carries no fuel this domain knows.");
        }

        var meterDay = new MeterDay(message.MPRN, message.ReadDate);
        var kept = await store.LoadAsync(meterDay, cancellationToken);
        var ledger = MeterDayLedger.Rehydrate(meterDay, kept.Select(recorded => recorded.Entry));

        switch (ledger.Record(ToReading(message), time.GetUtcNow()))
        {
            case RecordOutcome.Recorded recorded:
                var entry = new RecordedEntry(recorded.Entry, message.Provenance);
                await store.AppendAsync(entry, cancellationToken);
                await AnnounceAsync(entry, message.CorrelationId, cancellationToken);
                if (recorded.ReplacedMeterSerial is { } replaced)
                {
                    logger.LogWarning(
                        "{SupplyPointId} on {ReadDate} is reported by meter {MeterSerial} after meter {ReplacedMeterSerial}: " +
                        "an exchange, recorded on the one supply point series", message.MPRN, message.ReadDate,
                        message.MeterSerial, replaced);
                }

                break;

            case RecordOutcome.AlreadyRecorded already:
                await AnnounceAsync(kept.Single(recorded => recorded.Entry.Rrvn == already.Stored.Rrvn), message.CorrelationId,
                    cancellationToken);
                break;

            case RecordOutcome.AlreadySuperseded superseded:
                logger.LogWarning(
                    "{SupplyPointId} on {ReadDate}: RRVN {Rrvn} arrived after RRVN {EffectiveRrvn} was recorded, so it " +
                    "posts nothing", message.MPRN, message.ReadDate, message.RRVN, superseded.EffectiveRrvn);
                break;

            case RecordOutcome.ConflictingResend conflicting:
                RecordTelemetry.RecordConflictingResend();
                logger.LogWarning(
                    "{SupplyPointId} on {ReadDate}: RRVN {Rrvn} from meter {MeterSerial} in {ContentHash} differs from " +
                    "the RRVN kept from meter {KeptMeterSerial} in {KeptLedgerEntryId}; the first received is kept",
                    message.MPRN, message.ReadDate, message.RRVN, message.MeterSerial, message.Provenance.ContentHash,
                    conflicting.Stored.MeterSerial, conflicting.Stored.LedgerEntryId);
                break;
        }
    }

    private static MeterDayReading ToReading(IntervalReadingReceived message) => new()
    {
        ReadingId = message.ReadingId,
        MeterSerial = message.MeterSerial,
        Rrvn = message.RRVN,
        MeteringIntervalMinutes = message.MeteringIntervalMinutes,
        UomCode = message.UOMCode,
        Periods = [.. message.Periods.OrderBy(period => period.Timestamp).Select((period, index) => new EntryPeriod(
            index + 1,
            IntervalEnergy.ToKwh(period.Value, message.UOMCode, message.MeteringIntervalMinutes),
            period.StatusCode))],
    };

    // The event is built from the kept entry alone, so announcing it again yields the same id, time and values.
    private Task AnnounceAsync(RecordedEntry kept, string correlationId, CancellationToken cancellationToken)
    {
        var (entry, provenance) = kept;
        var recorded = new ConsumptionRecorded
        {
            EventId = DeterministicGuid.From(entry.LedgerEntryId),
            PublishingContext = ConsumptionTopology.PublishingContext,
            OccurredAtUtc = entry.PostedAtUtc,
            CorrelationId = correlationId,
            LedgerEntryId = entry.LedgerEntryId,
            SupplyPointId = entry.MeterDay.SupplyPointId,
            MeterSerial = entry.MeterSerial,
            ReadDate = entry.MeterDay.ReadDate,
            RRVN = entry.Rrvn,
            SupersedesRRVN = entry.SupersedesRrvn,
            EntryType = entry.EntryType.ToString(),
            PostedAtUtc = entry.PostedAtUtc,
            MeteringIntervalMinutes = entry.MeteringIntervalMinutes,
            UOMCode = entry.UomCode,
            Fuel = MeteringConsumptionTopology.Markets.FuelOf(provenance.Market)!,
            Reversal = entry.Reversal is { } reversal ? ToLeg(reversal) : null,
            Reading = ToLeg(entry.Reading),
            Provenance = provenance,
        };

        return publisher.PublishAsync(recorded, new EventMetadata
        {
            MessageId = entry.LedgerEntryId,
            CorrelationId = correlationId,
            SessionId = entry.MeterDay.SupplyPointId,
        }, cancellationToken);
    }

    private static LedgerLeg ToLeg(EntryLeg leg) =>
        new(leg.Rrvn, [.. leg.Periods.Select(period => new LedgerPeriod(period.Period, period.Kwh, period.Status))]);
}
