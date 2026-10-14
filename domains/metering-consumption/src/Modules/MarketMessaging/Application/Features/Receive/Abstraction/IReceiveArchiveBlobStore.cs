using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;

public interface IReceiveArchiveBlobStore
{
    Task<string> StoreAsync(SealedFile sealedFile, ReadOnlyMemory<byte> content, string fileName,
        DateTimeOffset receivedAt, CancellationToken cancellationToken);
}
