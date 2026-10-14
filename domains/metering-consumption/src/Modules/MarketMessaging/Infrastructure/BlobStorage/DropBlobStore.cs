using Azure;
using Azure.Storage.Blobs;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.MarketMessaging.Infrastructure.BlobStorage;

public sealed class DropBlobStore(BlobServiceClient blobService, IOptions<DropOptions> options) : IDropBlobStore
{
    private const int NotFound = 404;

    private BlobContainerClient Container => blobService.GetBlobContainerClient(options.Value.Container);

    public async Task<BinaryData?> OpenAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var content = await Container.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
            return content.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) =>
        Container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
