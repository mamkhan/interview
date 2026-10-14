using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Contracts;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Consumers;

// No sessions: a run of the V1 reader sends one event per supply point, and the supply point's row lock already orders a
// reload against Store's messages. With a session per MPRN and an idle wait, a run over every forecast meter would take
// hours instead of minutes.
public sealed class SupplyPointLoadedFromV1EventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<SupplyPointLoadedFromV1EventConsumer> logger)
    : EventConsumer<SupplyPointLoadedFromV1, SupplyPointLoadedFromV1EventHandler>(
        client,
        names.Resolve(PlatformTopology.Topics.SupplyPointLoadedFromV1),
        SupplyPointTopology.Subscriptions.SupplyPointLoadedFromV1,
        scopeFactory,
        logger,
        maxConcurrency: 4);
