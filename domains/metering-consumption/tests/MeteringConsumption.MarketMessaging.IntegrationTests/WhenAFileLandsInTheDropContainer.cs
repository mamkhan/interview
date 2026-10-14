using System.Globalization;
using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Contracts;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Infrastructure.BlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// The Receive role booted through the module's own registration on this class's scope: a file in the
// drop container and its BlobCreated event become a sealed file in the archive, a receipt on the bus and an
// empty drop container. The same file dropped again is a second receipt on one sealed file.
public sealed class WhenAFileLandsInTheDropContainer(FileLandedInTheDropContainerScenario landed)
    : IClassFixture<FileLandedInTheDropContainerScenario>
{
    private const string EventId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    private const string FileName = "DSO_MIM_343_1782352213415_3805737.xml";
    private const string PayloadSha256 = "5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724";

    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SealsTheFileUnderTheMarketAndHashPath() =>
        landed.ArchiveBlobs.Should().Equal($"roi-esb/{PayloadSha256}.xml");

    [Fact]
    public void StampsTheFileNameAndReceivedAtOnTheArchiveBlob()
    {
        landed.ArchiveMetadata[ArchiveBlobStore.FileNameMetadata].Should().Be(FileName);
        landed.ArchiveMetadata[ArchiveBlobStore.ReceivedAtMetadata].Should().Be(CreatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AnnouncesTheReceiptWithTheEventIdAsMessageId()
    {
        landed.FirstReceipt.MessageId.Should().Be(EventId);
        landed.FirstReceipt.CorrelationId.Should().Be(EventId);
        landed.FirstReceipt.Body.ToObjectFromJson<MarketMessageReceived>(landed.Json)!.Hash.Should().Be(PayloadSha256);
    }

    [Fact]
    public void RemovesTheDroppedFile() => landed.DroppedFilesAfterFirst.Should().BeEmpty();

    [Fact]
    public void AnnouncesTheSameFileDroppedAgainAsASecondReceiptOnOneSealedFile()
    {
        landed.SecondReceipt.Should().NotBeNull();
        landed.SecondReceipt!.MessageId.Should().Be(EventId);
        landed.ArchiveBlobsAfterSecond.Should().Equal($"roi-esb/{PayloadSha256}.xml");
    }
}
