using System.Net;
using MeteringConsumption.MarketMessaging;

namespace MeteringConsumption.AcceptanceTests.MarketMessaging.Scenarios;

// ESB sends one file of every message type the platform reads, each built for its own new supply point, all put in the
// running environment's drop container at once. Each file's receipt is read back with its outcome, along with whether
// its payload is in the archive and out of the drop container, and both hosts' health.
public sealed class EsbSentEveryMessageTypeScenario : IAsyncLifetime
{
    public static readonly string[] MessageTypeCodes =
        ["101", "105", "300", "300S", "301", "306", "307", "310", "320", "331", "332", "343"];

    public Dictionary<string, Landed> Files { get; } = [];

    public HttpStatusCode ProcessingHostHealth { get; private set; }

    public HttpStatusCode ReceiverHostHealth { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var environment = TargetEnvironment.Discover();
        var blobs = environment.Blobs();
        var drop = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        var archive = blobs.GetBlobContainerClient(MarketMessagingTopology.Containers.MarketMessageArchive);

        var landed = await Task.WhenAll(MessageTypeCodes.Select(async code =>
        {
            var identity = Guid.NewGuid();
            var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
            var fileName = $"DSO_MIM_{code}_{identity:N}_acceptance.xml";
            var payload = new MarketMessageXmlBuilder().WithMessageTypeCode(code).WithMprn(mprn).BuildBytes();
            await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);

            var receipt = await Receipts.WaitForTheOutcomeAsync(environment, fileName, ct);
            return new Landed(
                code,
                fileName,
                receipt,
                await archive.GetBlobClient(receipt.BlobPath).ExistsAsync(ct),
                await drop.GetBlobClient(fileName).ExistsAsync(ct));
        }));

        foreach (var file in landed)
        {
            Files[file.MessageTypeCode] = file;
        }

        using var http = new HttpClient();
        ProcessingHostHealth = (await http.GetAsync(new Uri(environment.MarketMsgUrl, "health"), ct)).StatusCode;
        ReceiverHostHealth = (await http.GetAsync(new Uri(environment.ReceiverUrl, "health"), ct)).StatusCode;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public sealed record Landed(string MessageTypeCode, string FileName, Receipts.ReceiptRow Receipt, bool IsArchived,
        bool IsStillInTheDropContainer);
}
