using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.Infrastructure.Consumers;

// Sessions are keyed by the MPRN, so one supply point's messages are handled one at a time while different
// supply points run in parallel. The V1 reader's nightly smart meter flag for every supply point arrives here too, so
// how many run at once, and how soon an idle one is let go, is ProfileOptions'.
public sealed class ProfileMeterConfigurationReceivedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IOptions<ProfileOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<ProfileMeterConfigurationReceivedEventConsumer> logger)
    : EventConsumer<MeterConfigurationReceived, ProfileMeterConfigurationReceivedEventHandler>(
        client,
        names.Resolve(MeteringConsumptionTopology.Topics.MeterConfigurationReceived),
        ConsumptionTopology.Subscriptions.ProfileMeterConfigurationReceived,
        scopeFactory,
        logger,
        sessionEnabled: true,
        maxConcurrency: options.Value.MaxConcurrentSessions,
        sessionIdleTimeout: options.Value.SessionIdleTimeout);
