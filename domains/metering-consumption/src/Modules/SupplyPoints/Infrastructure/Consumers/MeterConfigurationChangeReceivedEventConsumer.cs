using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Consumers;

// Sessions are keyed by the MPRN, so one supply point's changes are handled one at a time and in order, while different
// supply points run in parallel. Most sessions hold a single message, hence the short idle timeout.
public sealed class MeterConfigurationChangeReceivedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<MeterConfigurationChangeReceivedEventConsumer> logger)
    : EventConsumer<MeterConfigurationChangeReceived, MeterConfigurationChangeReceivedEventHandler>(
        client,
        names.Resolve(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived),
        SupplyPointTopology.Subscriptions.MeterConfigurationChangeReceived,
        scopeFactory,
        logger,
        sessionEnabled: true,
        maxConcurrency: 8,
        sessionIdleTimeout: TimeSpan.FromSeconds(5));
