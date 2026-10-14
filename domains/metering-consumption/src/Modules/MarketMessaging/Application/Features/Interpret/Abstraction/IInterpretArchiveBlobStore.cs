using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;

public interface IInterpretArchiveBlobStore
{
    Task<Stream> OpenAsync(string blobPath, CancellationToken cancellationToken);
}
