using System.Text.Json;

namespace MeteringConsumption.TestsShared;

// Builds the CloudEvents 1.0 body Event Grid delivers for Microsoft.Storage.BlobCreated, with the fields
// the Receive feature reads (id, time, subject, data.url, data.api) and defaults for a file dropped in the
// esb-drop container. The subject is the source of the container and blob name, as it is for the
// real event.
public sealed class BlobCreatedEventBuilder
{
    private const string StorageAccount = "https://acmedevmetrsa.blob.core.windows.net";

    private string _id = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    private DateTimeOffset _time = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private string _container = "esb-drop";
    private string _blobName = "DSO_MIM_343_1782352213415_3805737.xml";
    private string _api = "PutBlob";

    public BlobCreatedEventBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public BlobCreatedEventBuilder WithTime(DateTimeOffset time)
    {
        _time = time;
        return this;
    }

    public BlobCreatedEventBuilder WithContainer(string container)
    {
        _container = container;
        return this;
    }

    public BlobCreatedEventBuilder WithBlobName(string blobName)
    {
        _blobName = blobName;
        return this;
    }

    public BlobCreatedEventBuilder WithApi(string api)
    {
        _api = api;
        return this;
    }

    public string BuildJson() => JsonSerializer.Serialize(new
    {
        id = _id,
        source = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-acme-dev-metr/providers/Microsoft.Storage/storageAccounts/acmedevmetrsa",
        specversion = "1.0",
        type = "Microsoft.Storage.BlobCreated",
        subject = $"/blobServices/default/containers/{_container}/blobs/{_blobName}",
        time = _time.ToString("O"),
        data = new
        {
            api = _api,
            clientRequestId = "6d79dbfb-0e37-4fc4-981f-442c9ca65760",
            requestId = "831e1650-001e-001b-66ab-eeb76e000000",
            eTag = "0x8D4BCC2E4835CD0",
            contentType = "application/xml",
            contentLength = 524288,
            blobType = "BlockBlob",
            url = $"{StorageAccount}/{_container}/{_blobName}",
            sequencer = "00000000000004420000000000028963",
        },
    });
}
