using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Consumers;

// The map is resolved once here so a duplicate (market, type) claim stops the host rather than a delivery.
public sealed class InterpretMarketMessageReceivedEventConsumer : EventConsumer<MarketMessageReceived, InterpretMarketMessageReceivedEventHandler>
{
    public InterpretMarketMessageReceivedEventConsumer(
        ServiceBusClient client,
        EntityNames names,
        IServiceScopeFactory scopeFactory,
        ILogger<InterpretMarketMessageReceivedEventConsumer> logger)
        : base(client,
            names.Resolve(MarketMessagingTopology.Topics.MarketMessageReceived),
            MarketMessagingTopology.Subscriptions.InterpretMarketMessageReceived,
            scopeFactory,
            logger,
            maxConcurrency: 4,
            maxAutoLockRenewal: TimeSpan.FromMinutes(10))
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<MessageStrategy>();
    }
}
