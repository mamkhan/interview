using System.Xml;
using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.DTO;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using MeteringConsumption.MarketMessaging.Domain;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret;

public sealed class InterpretMarketMessageReceivedEventHandler(
    IInterpretArchiveBlobStore archive,
    MessageClassifiers classifiers,
    MessageStrategy interpreters,
    IEventPublisher publisher,
    TimeProvider time,
    ILogger<InterpretMarketMessageReceivedEventHandler> logger) : IEventHandler<MarketMessageReceived>
{
    public async Task HandleAsync(MarketMessageReceived message, CancellationToken cancellationToken)
    {
        await using var payload = await archive.OpenAsync(message.BlobPath, cancellationToken);

        string messageType;
        try
        {
            var classifier = classifiers.For(message.Market)
                ?? throw new MessageClassificationException($"No classifier is registered for market {message.Market}.");
            messageType = classifier.Classify(payload);
        }
        catch (MessageClassificationException exception)
        {
            await PublishFailedAsync(message, null, exception.Message, cancellationToken);
            return;
        }

        var interpreter = interpreters.Find(message.Market, messageType);
        
        if (interpreter is null)
        {
            await PublishProcessedAsync(message, new MarketMessageProcessed
            {
                EventId = OutcomeEventId(message),
                PublishingContext = MarketMessagingTopology.PublishingContext,
                OccurredAtUtc = time.GetUtcNow(),
                CorrelationId = message.CorrelationId,
                ReceiptId = message.ReceiptId,
                MessageType = messageType,
                Outcome = MarketMessageOutcomes.NoInterpreter,
            }, cancellationToken);
            return;
        }

        InterpretationOutcome outcome;
        try
        {
            payload.Position = 0;
            outcome = await interpreter.InterpretAsync(message, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is XmlException or MarketMessageRejectedException)
        {
            await PublishFailedAsync(message, messageType, exception.Message, cancellationToken);
            return;
        }

        await PublishProcessedAsync(message, new MarketMessageProcessed
        {
            EventId = OutcomeEventId(message),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = time.GetUtcNow(),
            CorrelationId = message.CorrelationId,
            ReceiptId = message.ReceiptId,
            MessageType = messageType,
            Outcome = MarketMessageOutcomes.Processed,
            Interpreter = interpreter.GetType().Name,
            ReadingsPublished = outcome.ReadingsPublished,
        }, cancellationToken);
    }

    private Task PublishProcessedAsync(MarketMessageReceived message, MarketMessageProcessed processed, CancellationToken cancellationToken)
    {
        logger.LogInformation("Receipt {ReceiptId} {Outcome} as {MessageType} ({ReadingsPublished} readings)",
            message.ReceiptId, processed.Outcome, processed.MessageType, processed.ReadingsPublished);
        return publisher.PublishAsync(processed, OutcomeMetadata(message), cancellationToken);
    }

    private Task PublishFailedAsync(MarketMessageReceived message, string? messageType, string reason, CancellationToken cancellationToken)
    {
        logger.LogWarning("Receipt {ReceiptId} failed: {Reason}", message.ReceiptId, reason);
        return publisher.PublishAsync(new MarketMessageFailed
        {
            EventId = OutcomeEventId(message),
            PublishingContext = MarketMessagingTopology.PublishingContext,
            OccurredAtUtc = time.GetUtcNow(),
            CorrelationId = message.CorrelationId,
            ReceiptId = message.ReceiptId,
            MessageType = messageType,
            Reason = reason,
        }, OutcomeMetadata(message), cancellationToken);
    }

    private static Guid OutcomeEventId(MarketMessageReceived message) => DeterministicGuid.From($"{message.ReceiptId}:outcome");

    private static EventMetadata OutcomeMetadata(MarketMessageReceived message) => new()
    {
        MessageId = message.ReceiptId,
        CorrelationId = message.CorrelationId,
        ApplicationProperties = new Dictionary<string, string>
        {
            [MeteringConsumptionTopology.MessageProperties.Market] = message.Market,
        },
    };
}
