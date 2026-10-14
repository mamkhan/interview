namespace MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;

public interface IDropBlobStore
{
    // Null when the blob is no longer there: an earlier delivery of the same event already moved it.
    Task<BinaryData?> OpenAsync(string blobName, CancellationToken cancellationToken);

    Task DeleteAsync(string blobName, CancellationToken cancellationToken);
}
