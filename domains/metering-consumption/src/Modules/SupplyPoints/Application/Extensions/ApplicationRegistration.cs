using BuildingBlocks.Commands;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.SupplyPoints.Application.Features.Provide;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Commands;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Commands;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.Application.Extensions;

public static class ApplicationRegistration
{
    public static IServiceCollection AddBaseApplication(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddStoreApplication(this IServiceCollection services)
    {
        services.AddScoped<MeterConfigurationChangeReceivedEventHandler>();
        services.AddScoped<ICommandHandler<MeterConfigurationChangeReceived, IReadOnlyList<RecordOutcomeKind>>, MeterConfigurationChangeCommandHandler>();

        return services;
    }

    public static IServiceCollection AddReloadApplication(this IServiceCollection services)
    {
        services.AddScoped<SupplyPointLoadedFromV1EventHandler>();
        services.AddScoped<ICommandHandler<SupplyPointLoadedFromV1, ReloadOutcome>, SupplyPointLoadedFromV1CommandHandler>();

        return services;
    }

    public static IServiceCollection AddProvideApplication(this IServiceCollection services)
    {
        services.AddSingleton<IMeterPointConfiguration, MeterPointConfigurationQuery>();

        return services;
    }
}
