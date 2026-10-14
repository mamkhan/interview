using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Consumers;

public sealed class AuditMarketMessageFailedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditMarketMessageFailedEventConsumer> logger)
    : EventConsumer<MarketMessageFailed, AuditMarketMessageFailedEventHandler>(
        client,
        names.Resolve(MarketMessagingTopology.Topics.MarketMessageFailed),
        MarketMessagingTopology.Subscriptions.AuditMarketMessageFailed,
        scopeFactory,
        logger);
