using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Consumers;

public sealed class AuditMarketMessageReceivedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditMarketMessageReceivedEventConsumer> logger)
    : EventConsumer<MarketMessageReceived, AuditMarketMessageReceivedEventHandler>(
        client,
        names.Resolve(MarketMessagingTopology.Topics.MarketMessageReceived),
        MarketMessagingTopology.Subscriptions.AuditMarketMessageReceived,
        scopeFactory,
        logger,
        maxConcurrency: 4);
