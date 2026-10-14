using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.MarketMessaging.Application.Features.Receive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Consumers;

public sealed class ReceiveBlobCreatedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<ReceiveBlobCreatedEventConsumer> logger)
    : EventConsumer<BlobCreated>(
        client,
        names.Resolve(MarketMessagingTopology.Queues.ReceiveBlobCreated),
        scopeFactory,
        logger);
