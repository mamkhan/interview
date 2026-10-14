using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BuildingBlocks.Messaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Domain;
using System.Globalization;

namespace MeteringConsumption.MarketMessaging.Infrastructure.BlobStorage;

public sealed class ArchiveBlobStore(BlobServiceClient blobService, EntityNames names) : IReceiveArchiveBlobStore, IInterpretArchiveBlobStore
{
    public const string FileNameMetadata = "fileName";
    public const string ReceivedAtMetadata = "receivedAt";

    // A write-once container rejects any overwrite with 409, identical bytes or not, so the conflict is
    // the success signal on a hash-named blob rather than a failure to report.
    private const int ImmutabilityPolicyConflict = 409;

    public async Task<Stream> OpenAsync(string blobPath, CancellationToken cancellationToken)
    {
        var blob = blobService
            .GetBlobContainerClient(names.Resolve(MarketMessagingTopology.Containers.MarketMessageArchive))
            .GetBlobClient(blobPath);
        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<string> StoreAsync(SealedFile sealedFile, ReadOnlyMemory<byte> content, string fileName,
        DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        var container = blobService.GetBlobContainerClient(names.Resolve(MarketMessagingTopology.Containers.MarketMessageArchive));
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(sealedFile.Path);
        try
        {
            await blob.UploadAsync(new BinaryData(content), new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                Metadata = new Dictionary<string, string>
                {
                    [FileNameMetadata] = fileName,
                    [ReceivedAtMetadata] = receivedAt.ToString("O", CultureInfo.InvariantCulture),
                },
            }, cancellationToken);
        }
        catch (RequestFailedException ex) when (
            ex.ErrorCode == BlobErrorCode.BlobAlreadyExists || ex.Status == ImmutabilityPolicyConflict)
        {
        }

        return sealedFile.Path;
    }
}
