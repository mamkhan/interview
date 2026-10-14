using AwesomeAssertions;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Audit;

// The receipt and its outcome travel on separate subscriptions, so the outcome can arrive first. The recorder asks
// again after each of its four waits (0.5, 1, 2 and 4 s, free on a clock that stands still): a receipt that lands in
// the meantime takes the outcome, and one that never lands fails the delivery so the bus brings it back later.
public sealed class WhenAnOutcomeOvertakesItsReceipt
{
    private static readonly DateTimeOffset ReachedAt = new(2026, 9, 16, 10, 0, 5, 456, TimeSpan.Zero);

    private readonly FakeMarketMessageRepository _repository = new();
    private readonly AuditMarketMessageProcessedEventHandler _handler;

    public WhenAnOutcomeOvertakesItsReceipt() =>
        _handler = new AuditMarketMessageProcessedEventHandler(new ReceiptOutcomeRecorder(_repository,
            new FixedTimeProvider(ReachedAt), NullLogger<ReceiptOutcomeRecorder>.Instance));

    [Fact]
    public async Task RecordsItOnceTheReceiptLands()
    {
        _repository.ReceiptLandsOnAttempt("r-late", 3);

        await _handler.HandleAsync(Processed("r-late"), TestContext.Current.CancellationToken);

        _repository.Outcomes.Should().ContainKey("r-late");
        _repository.OutcomeAttempts.Should().Be(3);
    }

    [Fact]
    public async Task FailsTheDeliveryAfterTheLastWaitWhenTheReceiptNeverLands()
    {
        var act = () => _handler.HandleAsync(Processed("r-missing"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ReceiptNotYetRecordedException>();
        _repository.OutcomeAttempts.Should().Be(5);
    }

    private static MarketMessageProcessed Processed(string receiptId) => new()
    {
        EventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = ReachedAt,
        CorrelationId = receiptId,
        ReceiptId = receiptId,
        MessageType = "343",
        Outcome = MarketMessageOutcomes.Processed,
    };
}
