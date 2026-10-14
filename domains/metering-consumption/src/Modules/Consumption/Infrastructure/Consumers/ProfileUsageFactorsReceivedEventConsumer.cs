using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeteringConsumption.Consumption.Infrastructure.Consumers;

// Sessions are keyed by the MPRN, so one supply point's messages are handled one at a time while different
// supply points run in parallel. Most sessions hold a single message, hence the short idle timeout.
public sealed class ProfileUsageFactorsReceivedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IServiceScopeFactory scopeFactory,
    ILogger<ProfileUsageFactorsReceivedEventConsumer> logger)
    : EventConsumer<UsageFactorsReceived, ProfileUsageFactorsReceivedEventHandler>(
        client,
        names.Resolve(MeteringConsumptionTopology.Topics.UsageFactorsReceived),
        ConsumptionTopology.Subscriptions.ProfileUsageFactorsReceived,
        scopeFactory,
        logger,
        sessionEnabled: true,
        maxConcurrency: 8,
        sessionIdleTimeout: TimeSpan.FromSeconds(5));
