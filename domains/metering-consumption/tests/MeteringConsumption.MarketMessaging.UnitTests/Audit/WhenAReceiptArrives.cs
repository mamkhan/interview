using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Audit;

// Every arrival is a row: the Receive feature's receipt becomes one, under its receipt id, and the same receipt
// delivered again leaves that row as it was.
public sealed class WhenAReceiptArrives
{
    private readonly FakeMarketMessageRepository _repository = new();
    private readonly AuditMarketMessageReceivedEventHandler _handler;

    public WhenAReceiptArrives() =>
        _handler = new AuditMarketMessageReceivedEventHandler(_repository,
            NullLogger<AuditMarketMessageReceivedEventHandler>.Instance);

    [Fact]
    public async Task RecordsTheArrivalWithNoOutcomeYet()
    {
        await _handler.HandleAsync(Received(), TestContext.Current.CancellationToken);

        _repository.Receipts["r-1"].Should().BeEquivalentTo(Receipts.For("r-1"));
    }

    [Fact]
    public async Task KeepsTheFirstRowWhenTheReceiptIsDeliveredAgain()
    {
        await _handler.HandleAsync(Received(), TestContext.Current.CancellationToken);
        var first = _repository.Receipts["r-1"];

        await _handler.HandleAsync(Received(), TestContext.Current.CancellationToken);

        _repository.Receipts.Should().ContainSingle().Which.Value.Should().BeSameAs(first);
    }

    private static MarketMessageReceived Received() => new()
    {
        EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        CorrelationId = "r-1",
        ReceiptId = "r-1",
        Market = "roi-esb",
        Fuel = "electricity",
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304",
        BlobPath = "roi-esb/e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304.xml",
    };
}

// The receipt row the Audit tests start from, with the literals of the real extract's arrival.
internal static class Receipts
{
    public static MarketMessage For(string receiptId) => new()
    {
        ReceiptId = receiptId,
        Market = "roi-esb",
        Fuel = "electricity",
        FileName = "DSO_MIM_343_1782352213415_3805737.xml",
        ReceivedAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
        Hash = "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304",
        BlobPath = "roi-esb/e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304.xml",
        CorrelationId = receiptId,
    };
}
