using AwesomeAssertions;
using Azure.Storage.Blobs;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.MarketMessaging.Domain;
using MeteringConsumption.MarketMessaging.Infrastructure.BlobStorage;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

public sealed class WhenStoringAFileInTheArchive : IAsyncLifetime
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private BlobServiceClient _blobService = null!;
    private ArchiveBlobStore _archive = null!;

    public async ValueTask InitializeAsync()
    {
        var endpoints = await SharedPlatformContainers.EnsureAsync(needsSql: false, needsServiceBus: false, needsAzurite: true);
        // Azurite rejects the SDK's newest service version, which is why production pins one too.
        _blobService = new BlobServiceClient(endpoints.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03));
        _archive = new ArchiveBlobStore(_blobService, new EntityNames(Options.Create(new EntityNamingOptions())));
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task StoresTheBlobUnderTheMarketAndHashPathWithTheOriginalBytes()
    {
        var content = RandomContent();
        var sealedFile = SealedFile.Of("roi-esb", content);

        var path = await _archive.StoreAsync(sealedFile, content, "a.xml", ReceivedAt, TestContext.Current.CancellationToken);

        path.Should().Be($"roi-esb/{sealedFile.Hash}.xml");
        var stored = await _blobService
            .GetBlobContainerClient(MarketMessagingTopology.Containers.MarketMessageArchive)
            .GetBlobClient(path)
            .DownloadContentAsync(TestContext.Current.CancellationToken);
        stored.Value.Content.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task SucceedsAndChangesNothingWhenReArchivingTheSameContent()
    {
        var content = RandomContent();
        var sealedFile = SealedFile.Of("roi-esb", content);

        var first = await _archive.StoreAsync(sealedFile, content, "a.xml", ReceivedAt, TestContext.Current.CancellationToken);
        var second = await _archive.StoreAsync(sealedFile, content, "a.xml", ReceivedAt, TestContext.Current.CancellationToken);

        second.Should().Be(first);
    }

    private static byte[] RandomContent() =>
        System.Text.Encoding.UTF8.GetBytes($"<MarketMessage nonce=\"{Guid.NewGuid():N}\"/>");
}
