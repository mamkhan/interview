using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Forecast;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Consumption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.Infrastructure.Consumers;

// Sessions are keyed by the supply point, so one supply point's readings are forecast one at a time and in order.
// Most sessions hold a single reading, hence the short idle timeout.
public sealed class ForecastConsumptionRecordedEventConsumer(
    ServiceBusClient client,
    EntityNames names,
    IOptions<ForecastOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<ForecastConsumptionRecordedEventConsumer> logger)
    : EventConsumer<ConsumptionRecorded, ForecastConsumptionRecordedEventHandler>(
        client,
        names.Resolve(MeteringConsumptionTopology.Topics.ConsumptionRecorded),
        ConsumptionTopology.Subscriptions.ForecastConsumptionRecorded,
        scopeFactory,
        logger,
        sessionEnabled: true,
        maxConcurrency: options.Value.MaxConcurrentSessions,
        sessionIdleTimeout: TimeSpan.FromSeconds(5));
