using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.Consumption;
using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Record.Abstraction;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.Consumers;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.Consumption.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.Infrastructure.Extensions;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddBaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEventTopics(map => map
            .Register<ConsumptionRecorded>(MeteringConsumptionTopology.Topics.ConsumptionRecorded)
            .Register<ConsumptionForecastUpdated>(MeteringConsumptionTopology.Topics.ConsumptionForecastUpdated));

        return services;
    }

    public static IServiceCollection AddRecordInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RecordOptions>()
            .BindConfiguration(RecordOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConsumptionLedgerStore, ParquetConsumptionLedgerStore>();

        services.AddHostedService<RecordIntervalReadingReceivedEventConsumer>();

        return services;
    }

    // The configuration in effect comes from SupplyPoints' IMeterPointConfiguration, which its Provide role registers:
    // a host that runs Forecast runs that role too.
    public static IServiceCollection AddForecastInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForecastOptions>()
            .BindConfiguration(ForecastOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ForecastOptions>>().Value.ToSettings());

        services.AddSingleton<IConsumptionLedgerReader, ParquetConsumptionLedgerReader>();
        services.AddSingleton<IConsumptionForecastStore>(new ConsumptionForecastRepository(configuration.ConsumptionConnectionString()));
        services.AddSingleton<ISupplyPointTariffReader>(new SupplyPointTariffReadRepository(configuration.ConsumptionConnectionString()));
        services.AddSingleton<IMarketReferenceData>(new MarketReferenceDataRepository(configuration.ReferenceDataConnectionString()));

        services.AddHostedService<ForecastConsumptionRecordedEventConsumer>();
        services.AddHostedService<SupersededForecastCleanupWorker>();

        return services;
    }

    public static IServiceCollection AddProfileInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProfileOptions>()
            .BindConfiguration(ProfileOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISupplyPointProfileRepository>(
            new SupplyPointProfileRepository(configuration.ConsumptionConnectionString()));

        services.AddHostedService<ProfileMeterConfigurationReceivedEventConsumer>();
        services.AddHostedService<ProfileUsageFactorsReceivedEventConsumer>();
        services.AddHostedService<ProfileTariffResolvedEventConsumer>();

        return services;
    }
}
