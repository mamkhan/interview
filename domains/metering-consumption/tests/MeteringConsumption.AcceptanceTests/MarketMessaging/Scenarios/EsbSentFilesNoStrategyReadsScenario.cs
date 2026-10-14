using System.Text;
using MeteringConsumption.MarketMessaging;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;

// ESB sends two files the platform cannot turn into events: a well-formed file of a message type no strategy reads
// (017), and a file that is not XML at all. Both are put in the drop container at once and their receipts read back.
public sealed class EsbSentFilesNoStrategyReadsScenario : IAsyncLifetime
{
    public Receipts.ReceiptRow UnreadType { get; private set; } = null!;

    public Receipts.ReceiptRow Unreadable { get; private set; } = null!;

    public bool UnreadTypeIsArchived { get; private set; }

    public bool UnreadableIsArchived { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = TargetEnvironment.Discover();
        var blobs = environment.Blobs();
        var drop = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        var archive = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.MarketMessageArchive);

        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var unreadTypeName = $"DSO_MIM_017_{identity:N}_acceptance.xml";
        var unreadableName = $"DSO_MIM_343_{identity:N}_unreadable_acceptance.xml";

        await Task.WhenAll(
            drop.GetBlobClient(unreadTypeName).UploadAsync(BinaryData.FromBytes(
                new MarketMessageXmlBuilder().WithMessageTypeCode("017").WithMprn(mprn).BuildBytes()), overwrite: false, ct),
            drop.GetBlobClient(unreadableName).UploadAsync(BinaryData.FromBytes(
                Encoding.UTF8.GetBytes($"not an ESB file {identity:N}")), overwrite: false, ct));

        UnreadType = await Receipts.WaitForTheOutcomeAsync(environment, unreadTypeName, ct);
        Unreadable = await Receipts.WaitForTheOutcomeAsync(environment, unreadableName, ct);
        UnreadTypeIsArchived = await archive.GetBlobClient(UnreadType.BlobPath).ExistsAsync(ct);
        UnreadableIsArchived = await archive.GetBlobClient(Unreadable.BlobPath).ExistsAsync(ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
