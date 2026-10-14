using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.Consumption.Infrastructure.DataLake;

// Lists only the months the window can fall in, by the read-date prefix of the file names, so the cost does not grow
// with the supply point's history. The highest version of each read date comes from the names, and only the recent
// files are read. A version's reading leg is its day in full, so reversal legs are not read.
public sealed class ParquetConsumptionLedgerReader(
    [FromKeyedServices(RecordOptions.LedgerConnectionName)] BlobServiceClient blobService,
    EntityNames names) : IConsumptionLedgerReader
{
    public async Task<IReadOnlyList<LedgerDay>> ReadRecentDaysAsync(
        string supplyPointId, DateOnly asOf, int days, CancellationToken cancellationToken)
    {
        var container = blobService.GetBlobContainerClient(names.Resolve(MeteringConsumptionTopology.Containers.ConsumptionLedger));
        var current = new Dictionary<DateOnly, (int Rrvn, string Name)>();
        var listedFrom = FirstOfMonth(asOf.AddDays(-(days - 1)));
        await ListAsync(container, supplyPointId, listedFrom, FirstOfMonth(asOf), current, cancellationToken);

        // The latest read can be older than the window from asOf: a revision of an old day on a meter that stopped
        // reading. The earlier months its window needs are then listed too, and the whole folder only when the
        // recent months hold nothing.
        if (current.Count == 0)
        {
            await ListAsync(container, LedgerPath.SupplyPointFolder(supplyPointId), current, cancellationToken);
        }
        else if (current.Keys.Max().AddDays(-(days - 1)) is var needed && needed < listedFrom)
        {
            await ListAsync(container, supplyPointId, FirstOfMonth(needed), listedFrom.AddMonths(-1), current, cancellationToken);
        }

        if (current.Count == 0)
        {
            return [];
        }

        var from = current.Keys.Max().AddDays(-(days - 1));
        var recent = current.Where(day => day.Key >= from).OrderBy(day => day.Key);
        return await Task.WhenAll(recent.Select(day => ReadDayAsync(container, day.Key, day.Value.Rrvn, day.Value.Name, cancellationToken)));
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static async Task ListAsync(
        BlobContainerClient container, string supplyPointId, DateOnly firstMonth, DateOnly lastMonth,
        Dictionary<DateOnly, (int Rrvn, string Name)> current, CancellationToken cancellationToken)
    {
        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            await ListAsync(container, LedgerPath.MonthPrefix(supplyPointId, month), current, cancellationToken);
        }
    }

    private static async Task ListAsync(
        BlobContainerClient container, string prefix, Dictionary<DateOnly, (int Rrvn, string Name)> current,
        CancellationToken cancellationToken)
    {
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            if (LedgerPath.TryParse(blob.Name, out var readDate, out var rrvn)
                && (!current.TryGetValue(readDate, out var kept) || rrvn > kept.Rrvn))
            {
                current[readDate] = (rrvn, blob.Name);
            }
        }
    }

    private static async Task<LedgerDay> ReadDayAsync(
        BlobContainerClient container, DateOnly readDate, int rrvn, string name, CancellationToken cancellationToken)
    {
        var content = await container.GetBlobClient(name).DownloadContentAsync(cancellationToken);
        using var stream = content.Value.Content.ToStream();
        var rows = await LedgerFile.ReadAsync(stream, cancellationToken);
        var reading = rows.Where(row => row.Leg == LedgerRow.ReadingLeg)
            .Select(row => new EntryPeriod(row.Period, row.Kwh, row.Status));
        return new LedgerDay(ReadDay.FromPeriods(readDate, reading), rows[0].LedgerEntryId, rrvn);
    }
}
