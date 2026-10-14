using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using Growth.Contracts;
using Growth.Contracts.Events;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.Infrastructure.Consumers;

// Sessions are keyed by the MPRN, so one supply point's tariff values are handled one at a time while different
// supply points run in parallel. How many run at once, and how soon an idle one is let go, is ProfileOptions'.
public sealed class ProfileTariffResolvedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IOptions<ProfileOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<ProfileTariffResolvedEventConsumer> logger)
    : EventConsumer<TariffResolved, ProfileTariffResolvedEventHandler>(
        client,
        names.Resolve(GrowthTopology.Topics.TariffResolved),
        ConsumptionTopology.Subscriptions.ProfileTariffResolved,
        scopeFactory,
        logger,
        sessionEnabled: true,
        maxConcurrency: options.Value.MaxConcurrentSessions,
        sessionIdleTimeout: options.Value.SessionIdleTimeout);
