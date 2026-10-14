using MeteringConsumption.Consumption.Application.Features.Forecast;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Application.Features.Record;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.Consumption.Application.Extensions;

public static class ApplicationRegistration
{
    public static IServiceCollection AddBaseApplication(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddRecordApplication(this IServiceCollection services)
    {
        services.AddScoped<RecordIntervalReadingReceivedEventHandler>();

        return services;
    }

    public static IServiceCollection AddForecastApplication(this IServiceCollection services)
    {
        services.AddScoped<ForecastConsumptionRecordedEventHandler>();

        return services;
    }

    public static IServiceCollection AddProfileApplication(this IServiceCollection services)
    {
        services.AddScoped<ProfileMeterConfigurationReceivedEventHandler>();
        services.AddScoped<ProfileUsageFactorsReceivedEventHandler>();
        services.AddScoped<ProfileTariffResolvedEventHandler>();

        return services;
    }
}
