using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.Contracts;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The ledger store and reader on one test class's scope: its own consumption-ledger container on the shared
// Azurite, resolved through the same entity naming the host uses.
internal sealed class LedgerOnAzurite
{
    private LedgerOnAzurite(
        BlobContainerClient container, ParquetConsumptionLedgerStore store, ParquetConsumptionLedgerReader reader)
    {
        Container = container;
        Store = store;
        Reader = reader;
    }

    public BlobContainerClient Container { get; }

    public ParquetConsumptionLedgerStore Store { get; }

    public ParquetConsumptionLedgerReader Reader { get; }

    public static async Task<LedgerOnAzurite> CreateAsync(string testClass, CancellationToken ct)
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, testClass, new SuiteResources
        {
            BlobContainers = [MeteringConsumptionTopology.Containers.ConsumptionLedger],
        }, ct);

        var blobs = new BlobServiceClient(scope.Endpoints.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
        var names = new EntityNames(Options.Create(new EntityNamingOptions { Prefix = $"{scope.Id}-" }));

        return new LedgerOnAzurite(
            blobs.GetBlobContainerClient(scope.BlobContainer(MeteringConsumptionTopology.Containers.ConsumptionLedger)),
            new ParquetConsumptionLedgerStore(blobs, names),
            new ParquetConsumptionLedgerReader(blobs, names));
    }

    public async Task<List<string>> FilesAsync(CancellationToken ct)
    {
        var names = new List<string>();
        await foreach (var blob in Container.GetBlobsAsync(cancellationToken: ct))
        {
            names.Add(blob.Name);
        }

        return names;
    }
}
