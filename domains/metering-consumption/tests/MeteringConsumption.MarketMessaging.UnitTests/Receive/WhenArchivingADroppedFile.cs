using System.Text.Json;
using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Receive;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.MarketMessaging.UnitTests.Receive;

// A file lands in the drop container and its BlobCreated event arrives: the bytes are sealed under the
// market and hash, the receipt is announced with the event id as its identity, and the dropped file is
// removed last. Frozen literals: the payload is the XML builder's default for MPRN 10900000343; its
// SHA-256 was derived offline: the bytes the builder writes, saved to a file, `shasum -a 256` over it.
public class WhenArchivingADroppedFile
{
    private const string EventId = "9aeb0fdf-c01e-0131-0922-9eb54906e209";
    private const string FileName = "DSO_MIM_343_1782352213415_3805737.xml";
    private const string PayloadSha256 = "5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 3, TimeSpan.Zero);

    private readonly CallLog _log = new();
    private readonly FakeDropContainer _drop;
    private readonly RecordingArchive _archive;
    private readonly RecordingPublisher _publisher;
    private readonly ReceiveBlobCreatedEventHandler _handler;
    private readonly byte[] _payload = new MarketMessageXmlBuilder().WithMprn("10900000343").BuildBytes();

    public WhenArchivingADroppedFile()
    {
        _drop = new FakeDropContainer(_log);
        _archive = new RecordingArchive(_log);
        _publisher = new RecordingPublisher(_log);
        _drop.Blobs[FileName] = _payload;
        _handler = new ReceiveBlobCreatedEventHandler(_drop, _archive, _publisher,
            Options.Create(new DropOptions { Container = "esb-drop", Market = "roi-esb", Fuel = "electricity" }),
            new FixedTimeProvider(Now), NullLogger<ReceiveBlobCreatedEventHandler>.Instance);
    }

    private static BlobCreated Event(string id = EventId, string blobName = FileName) =>
        JsonSerializer.Deserialize<BlobCreated>(
            new BlobCreatedEventBuilder().WithId(id).WithBlobName(blobName).WithTime(CreatedAt).BuildJson(), Web)!;

    private MarketMessageReceived Receipt => (MarketMessageReceived)_publisher.Published.Single().Event;

    [Fact]
    public async Task ArchivesUnderTheMarketAndHashPath()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        var stored = _archive.Stored.Should().ContainSingle().Subject;
        stored.File.Path.Should().Be($"roi-esb/{PayloadSha256}.xml");
        stored.FileName.Should().Be(FileName);
        stored.ReceivedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public async Task PublishesTheReceiptWithTheEventIdAsReceiptId()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        Receipt.ReceiptId.Should().Be(EventId);
        Receipt.Hash.Should().Be(PayloadSha256);
        Receipt.BlobPath.Should().Be($"roi-esb/{PayloadSha256}.xml");
        Receipt.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task UsesTheReceiptIdAsMessageIdAndCorrelationId()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        var metadata = _publisher.Published.Single().Metadata;
        metadata.MessageId.Should().Be(EventId);
        metadata.CorrelationId.Should().Be(EventId);
        Receipt.CorrelationId.Should().Be(EventId);
    }

    [Fact]
    public async Task StampsTheBlobCreatedTimeAsReceivedAt()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        Receipt.ReceivedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public async Task CarriesTheMarketFuelAndFileName()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        Receipt.Market.Should().Be("roi-esb");
        Receipt.Fuel.Should().Be("electricity");
        Receipt.FileName.Should().Be(FileName);
        _publisher.Published.Single().Metadata.ApplicationProperties.Should()
            .Equal(new Dictionary<string, string> { ["market"] = "roi-esb" });
    }

    [Fact]
    public async Task CarriesNoMessageType()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        typeof(MarketMessageReceived).GetProperty("MessageType").Should().BeNull();
        _publisher.Published.Single().Metadata.ApplicationProperties.Should().NotContainKey("messageType");
    }

    [Fact]
    public async Task DeletesTheDroppedFileLast()
    {
        await _handler.HandleAsync(Event(), CancellationToken.None);

        _log.Calls.Should().Equal("open", "archive", "publish", "delete");
        _drop.Deleted.Should().Equal(FileName);
    }

    [Fact]
    public async Task DoesNotDeleteWhenPublishingFails()
    {
        _publisher.Throws = new TimeoutException("bus unavailable");

        var act = () => _handler.HandleAsync(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        _drop.Deleted.Should().BeEmpty();
        _archive.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task DoesNotPublishOrDeleteWhenArchivingFails()
    {
        _archive.Throws = new TimeoutException("storage unavailable");

        var act = () => _handler.HandleAsync(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        _publisher.Published.Should().BeEmpty();
        _drop.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task TreatsAnIdenticalFileAlreadyInTheArchiveAsSealed()
    {
        // The archive reports success for bytes it already holds; the receipt is still announced and the
        // dropped copy still removed, because this arrival is a receipt of its own.
        await _handler.HandleAsync(Event(), CancellationToken.None);
        _drop.Blobs["DSO_MIM_343_9999999999999_0000001.xml"] = _payload;

        await _handler.HandleAsync(Event("second-event", "DSO_MIM_343_9999999999999_0000001.xml"), CancellationToken.None);

        _archive.Stored.Should().HaveCount(2).And.AllSatisfy(s => s.File.Hash.Should().Be(PayloadSha256));
        _publisher.Published.Select(p => p.Metadata.MessageId).Should().Equal(EventId, "second-event");
        _drop.Deleted.Should().Equal(FileName, "DSO_MIM_343_9999999999999_0000001.xml");
    }

    [Fact]
    public async Task CompletesQuietlyWhenTheDroppedFileIsAlreadyGone()
    {
        _drop.Blobs.Clear();

        await _handler.HandleAsync(Event(), CancellationToken.None);

        _archive.Stored.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
        _drop.Deleted.Should().BeEmpty();
    }
}
