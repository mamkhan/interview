using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.Consumption.Infrastructure.DataLake;

// One file per entry, so a meter-day loads by listing its day's prefix.
// A single upload is one atomic Put Blob: an entry's two legs land together or not at all.
public sealed class ParquetConsumptionLedgerStore(
    [FromKeyedServices(RecordOptions.LedgerConnectionName)] BlobServiceClient blobService,
    EntityNames names) : IConsumptionLedgerStore
{
    private BlobContainerClient Container =>
        blobService.GetBlobContainerClient(names.Resolve(MeteringConsumptionTopology.Containers.ConsumptionLedger));

    public async Task<IReadOnlyList<RecordedEntry>> LoadAsync(MeterDay meterDay, CancellationToken cancellationToken)
    {
        var container = Container;
        var entries = new List<RecordedEntry>();
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, LedgerPath.DayPrefix(meterDay.SupplyPointId, meterDay.ReadDate), cancellationToken))
        {
            var content = await container.GetBlobClient(blob.Name).DownloadContentAsync(cancellationToken);
            using var stream = content.Value.Content.ToStream();
            var rows = await LedgerFile.ReadAsync(stream, cancellationToken);
            entries.Add(ToEntry(meterDay, rows));
        }

        return [.. entries.OrderBy(recorded => recorded.Entry.Rrvn)];
    }

    public async Task AppendAsync(RecordedEntry recorded, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await LedgerFile.WriteAsync(ToRows(recorded), buffer, cancellationToken);
        buffer.Position = 0;

        var entry = recorded.Entry;
        await Container.GetBlobClient(LedgerPath.File(entry.MeterDay.SupplyPointId, entry.MeterDay.ReadDate, entry.Rrvn))
            .UploadAsync(buffer, overwrite: true, cancellationToken);
    }

    private static IEnumerable<LedgerRow> ToRows(RecordedEntry recorded)
    {
        var (entry, provenance) = recorded;
        var fuel = MeteringConsumptionTopology.Markets.FuelOf(provenance.Market)
            ?? throw new LedgerEntryRejectedException($"Market {provenance.Market} carries no fuel this domain knows.");

        var halfHours = (entry.Reversal?.Periods ?? []).Select(period => (Leg: LedgerRow.ReversalLeg, Period: period))
            .Concat(entry.Reading.Periods.Select(period => (Leg: LedgerRow.ReadingLeg, Period: period)));

        return halfHours.Select(halfHour => new LedgerRow
        {
            LedgerEntryId = entry.LedgerEntryId,
            SupplyPointId = entry.MeterDay.SupplyPointId,
            MeterSerial = entry.MeterSerial,
            ReadDate = entry.MeterDay.ReadDate,
            EntryType = entry.EntryType.ToString(),
            Rrvn = entry.Rrvn,
            SupersedesRrvn = entry.SupersedesRrvn,
            Leg = halfHour.Leg,
            PostedAtUtc = entry.PostedAtUtc.UtcDateTime,
            Period = halfHour.Period.Period,
            Kwh = halfHour.Period.Kwh,
            Status = halfHour.Period.Status,
            MeteringIntervalMinutes = entry.MeteringIntervalMinutes,
            UomCode = entry.UomCode,
            SourceMessageType = provenance.SourceMessageType,
            MarketTimestamp = provenance.MarketTimestamp.UtcDateTime,
            BlobPath = provenance.BlobPath,
            ContentHash = provenance.ContentHash,
            ReceivedFileName = provenance.ReceivedFileName,
            Fuel = fuel,
            Market = provenance.Market,
            Producer = ConsumptionTopology.PublishingContext,
        });
    }

    private static RecordedEntry ToEntry(MeterDay meterDay, IList<LedgerRow> rows)
    {
        var first = rows[0];
        var entry = new LedgerEntry
        {
            LedgerEntryId = first.LedgerEntryId,
            MeterDay = meterDay,
            MeterSerial = first.MeterSerial,
            EntryType = Enum.Parse<EntryType>(first.EntryType),
            Rrvn = first.Rrvn,
            SupersedesRrvn = first.SupersedesRrvn,
            PostedAtUtc = Utc(first.PostedAtUtc),
            MeteringIntervalMinutes = first.MeteringIntervalMinutes,
            UomCode = first.UomCode,
            Reversal = first.SupersedesRrvn is { } superseded ? Leg(superseded, rows, LedgerRow.ReversalLeg) : null,
            Reading = Leg(first.Rrvn, rows, LedgerRow.ReadingLeg),
        };
        var provenance = new ReadingProvenance(
            first.Market,
            first.SourceMessageType,
            Utc(first.MarketTimestamp),
            first.BlobPath,
            first.ContentHash,
            first.ReceivedFileName);
        return new RecordedEntry(entry, provenance);
    }

    private static DateTimeOffset Utc(DateTime timestamp) => new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private static EntryLeg Leg(int rrvn, IEnumerable<LedgerRow> rows, string leg) =>
        new(rrvn, [.. rows.Where(row => row.Leg == leg).OrderBy(row => row.Period)
            .Select(row => new EntryPeriod(row.Period, row.Kwh, row.Status))]);
}
