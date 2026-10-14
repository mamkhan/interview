using BuildingBlocks.Commands;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.Infrastructure.Consumers;
using MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.Infrastructure.Extensions;

public static class InfrastructureRegistration
{
    // The topics the module reads: the one Market Messaging publishes SupplyPoints' event to, and the platform's the V1
    // reader publishes to. Registering them also gives the consumers the entity names they resolve their topics through.
    public static IServiceCollection AddBaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEventTopics(map => map
            .Register<MeterConfigurationChangeReceived>(MeteringConsumptionTopology.Topics.MeterConfigurationChangeReceived)
            .Register<SupplyPointLoadedFromV1>(PlatformTopology.Topics.SupplyPointLoadedFromV1));

        return services;
    }

    public static IServiceCollection AddStoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddUnitOfWork(configuration.SupplyPointConnectionString());
        services.AddCommandRunner();
        services.AddScoped<ISupplyPointRepository, SupplyPointRepository>();

        services.AddHostedService<MeterConfigurationChangeReceivedEventConsumer>();

        return services;
    }

    public static IServiceCollection AddReloadInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddUnitOfWork(configuration.SupplyPointConnectionString());
        services.AddCommandRunner();
        services.AddScoped<ISupplyPointReloadRepository, SupplyPointRepository>();

        services.AddHostedService<SupplyPointLoadedFromV1EventConsumer>();

        return services;
    }

    public static IServiceCollection AddProvideInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISupplyPointReader>(new SupplyPointReadRepository(configuration.SupplyPointConnectionString()));

        return services;
    }
}
