using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Consumers;

public sealed class AuditMarketMessageProcessedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditMarketMessageProcessedEventConsumer> logger)
    : EventConsumer<MarketMessageProcessed, AuditMarketMessageProcessedEventHandler>(
        client,
        names.Resolve(MarketMessagingTopology.Topics.MarketMessageProcessed),
        MarketMessagingTopology.Subscriptions.AuditMarketMessageProcessed,
        scopeFactory,
        logger);
