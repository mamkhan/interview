using AwesomeAssertions;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.MarketMessaging.UnitTests.Audit;

// The Interpret feature's two outcomes reach a receipt that is already recorded: each is written to it once, with the
// moment the outcome was reached as its processing time.
public sealed class WhenAnOutcomeArrivesForItsReceipt
{
    private static readonly DateTimeOffset ReachedAt = new(2026, 9, 16, 10, 0, 5, 456, TimeSpan.Zero);

    private readonly FakeMarketMessageRepository _repository = new();
    private readonly ReceiptOutcomeRecorder _recorder;

    public WhenAnOutcomeArrivesForItsReceipt()
    {
        _recorder = new ReceiptOutcomeRecorder(_repository, new FixedTimeProvider(ReachedAt),
            NullLogger<ReceiptOutcomeRecorder>.Instance);
        _repository.Receipts["r-1"] = Receipts.For("r-1");
    }

    [Fact]
    public async Task RecordsTheProcessedOutcomeWithItsTypeInterpreterAndReadings()
    {
        await new AuditMarketMessageProcessedEventHandler(_recorder).HandleAsync(new MarketMessageProcessed
        {
            EventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = ReachedAt,
            CorrelationId = "r-1",
            ReceiptId = "r-1",
            MessageType = "343",
            Outcome = MarketMessageOutcomes.Processed,
            Interpreter = "MM343Strategy",
            ReadingsPublished = 2,
        }, TestContext.Current.CancellationToken);

        _repository.Outcomes["r-1"].Should().Be(new ReceiptOutcome
        {
            MessageType = "343",
            Outcome = "processed",
            Interpreter = "MM343Strategy",
            ReadingsPublished = 2,
            ProcessedAt = ReachedAt,
        });
    }

    [Fact]
    public async Task RecordsAFailureWithItsReason()
    {
        await new AuditMarketMessageFailedEventHandler(_recorder).HandleAsync(new MarketMessageFailed
        {
            EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = ReachedAt,
            CorrelationId = "r-1",
            ReceiptId = "r-1",
            Reason = "The file has no MessageHeader element.",
        }, TestContext.Current.CancellationToken);

        _repository.Outcomes["r-1"].Should().Be(new ReceiptOutcome
        {
            MessageType = null,
            Outcome = "failed",
            FailureReason = "The file has no MessageHeader element.",
            ProcessedAt = ReachedAt,
        });
    }

    [Fact]
    public async Task AsksTheStoreOnce()
    {
        await new AuditMarketMessageFailedEventHandler(_recorder).HandleAsync(new MarketMessageFailed
        {
            EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PublishingContext = "metering-consumption.market-messaging",
            OccurredAtUtc = ReachedAt,
            CorrelationId = "r-1",
            ReceiptId = "r-1",
            Reason = "The file has no MessageHeader element.",
        }, TestContext.Current.CancellationToken);

        _repository.OutcomeAttempts.Should().Be(1);
    }
}
