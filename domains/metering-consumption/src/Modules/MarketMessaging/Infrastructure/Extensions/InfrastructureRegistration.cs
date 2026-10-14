using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Infrastructure.Configuration;
using MeteringConsumption.MarketMessaging.Infrastructure.Consumers;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using MeteringConsumption.MarketMessaging.Infrastructure.Persistence.Repositories;
using MeteringConsumption.MarketMessaging.Infrastructure.BlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.MarketMessaging.Infrastructure.Extensions;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddBaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEventTopics(map => map
            .Register<MarketMessageReceived>(MarketMessagingTopology.Topics.MarketMessageReceived)
            .Register<MarketMessageProcessed>(MarketMessagingTopology.Topics.MarketMessageProcessed)
            .Register<MarketMessageFailed>(MarketMessagingTopology.Topics.MarketMessageFailed)
            .Register<IntervalReadingReceived>(MeteringConsumptionTopology.Topics.IntervalReadingReceived)
            .Register<UsageFactorsReceived>(MeteringConsumptionTopology.Topics.UsageFactorsReceived)
            .Register<MeterConfigurationReceived>(MeteringConsumptionTopology.Topics.MeterConfigurationReceived)
            .Register<MeterConfigurationChangeReceived>(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived));


        return services;
    }

    public static IServiceCollection AddReceiveInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEventTopics(map => map
            .Register<MarketMessageReceived>(MarketMessagingTopology.Topics.MarketMessageReceived));

        services.AddOptions<DropOptions>()
            .BindConfiguration(DropOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDropBlobStore, DropBlobStore>();
        services.AddSingleton<IReceiveArchiveBlobStore, ArchiveBlobStore>();

        services.AddHostedService<ReceiveBlobCreatedEventConsumer>();

        return services;
    }

    public static IServiceCollection AddInterpretInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        

        services.AddSingleton<IInterpretArchiveBlobStore, ArchiveBlobStore>();

        services.AddHostedService<InterpretMarketMessageReceivedEventConsumer>();

        return services;
    }

    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMarketMessageRepository>(new MarketMessageRepository(configuration.MarketMessagingConnectionString()));

        services.AddHostedService<AuditMarketMessageReceivedEventConsumer>();
        services.AddHostedService<AuditMarketMessageProcessedEventConsumer>();
        services.AddHostedService<AuditMarketMessageFailedEventConsumer>();

        return services;
    }
}
