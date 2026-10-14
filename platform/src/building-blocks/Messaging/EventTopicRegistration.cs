using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Messaging;

/// <summary>Lets every module co-hosted in one process contribute its own topic registrations to the
/// single <see cref="EventTopicMap"/> the publisher reads.</summary>
public static class EventTopicRegistration
{
    public static IServiceCollection AddEventTopics(this IServiceCollection services, Action<EventTopicMap> register)
    {
        services.TryAddSingleton(provider =>
        {
            var map = new EventTopicMap();
            foreach (var registration in provider.GetServices<EventTopicRegistrar>())
            {
                registration.Register(map);
            }

            return map;
        });
        services.AddOptions<EntityNamingOptions>().BindConfiguration(EntityNamingOptions.Section);
        services.TryAddSingleton<EntityNames>();
        services.TryAddSingleton<IEventPublisher, ServiceBusEventPublisher>();
        services.AddSingleton(new EventTopicRegistrar(register));
        return services;
    }

    private sealed record EventTopicRegistrar(Action<EventTopicMap> Register);
}
