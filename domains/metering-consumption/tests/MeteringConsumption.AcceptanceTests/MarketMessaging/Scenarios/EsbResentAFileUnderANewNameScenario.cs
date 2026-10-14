using Azure.Storage.Blobs.Models;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.Contracts;
using MeteringConsumption.MarketMessaging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;

// ESB sends one MM343 for a new supply point's 2026-06-24, then sends the same bytes again under another name once the
// first has its outcome. A revision of the same meter-day follows the resend: a supply point's readings are kept one
// at a time and in order, so once the revision is in the ledger the resend has been kept or ignored, and the ledger
// can be read without waiting out a timeout.
public sealed class EsbResentAFileUnderANewNameScenario : IAsyncLifetime
{
    private static readonly DateOnly ReadDate = new(2026, 6, 24);

    public string FirstFileName { get; private set; } = "";

    public Receipts.ReceiptRow First { get; private set; } = null!;

    public Receipts.ReceiptRow Resent { get; private set; } = null!;

    public IReadOnlyList<string> ArchivedUnderTheHash { get; private set; } = [];

    public IReadOnlyList<RecordedEntry> Ledger { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = TargetEnvironment.Discover();
        var blobs = environment.Blobs();
        var drop = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        var archive = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.MarketMessageArchive);
        var ledger = new ParquetConsumptionLedgerStore(environment.Ledger(),
            new EntityNames(Options.Create(new EntityNamingOptions())));

        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var payload = new MarketMessageXmlBuilder().WithMprn(mprn).WithReadDate(ReadDate).WithRrvn(1).BuildBytes();

        FirstFileName = $"DSO_MIM_343_{identity:N}_first_acceptance.xml";
        await drop.GetBlobClient(FirstFileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);
        First = await Receipts.WaitForTheOutcomeAsync(environment, FirstFileName, ct);

        var resentFileName = $"DSO_MIM_343_{identity:N}_resent_acceptance.xml";
        await drop.GetBlobClient(resentFileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);
        Resent = await Receipts.WaitForTheOutcomeAsync(environment, resentFileName, ct);

        var revision = new MarketMessageXmlBuilder().WithMprn(mprn).WithReadDate(ReadDate).WithRrvn(2)
            .WithIntervalValue(0.4m).BuildBytes();
        await drop.GetBlobClient($"DSO_MIM_343_{identity:N}_revision_acceptance.xml")
            .UploadAsync(BinaryData.FromBytes(revision), overwrite: false, ct);

        var meterDay = new MeterDay(mprn, ReadDate);
        Ledger = await Polling.UntilAsync(
            token => ledger.LoadAsync(meterDay, token),
            entries => entries.Any(recorded => recorded.Entry.Rrvn == 2),
            entries => $"the ledger for {mprn} on {ReadDate} holds RRVNs [{string.Join(", ", entries.Select(e => e.Entry.Rrvn))}], not the revision.",
            ct);

        var archived = new List<string>();
        await foreach (var blob in archive.GetBlobsAsync(BlobTraits.None, BlobStates.None,
                           $"{MeteringConsumptionTopology.Markets.RoiEsb}/{First.Hash}", ct))
        {
            archived.Add(blob.Name);
        }

        ArchivedUnderTheHash = archived;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
