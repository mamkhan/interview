using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

public static class TimeProviderExtensions
{
    // Try, not Add: a host's composition must not displace a clock its caller already registered, which is
    // how every test drives time. Scoped, because a delivery may carry its own AsOf; a scope nobody stamps
    // reads the system clock.
    public static void TryAddTimeProvider(this IServiceCollection services)
    {
        services.TryAddScoped<MessageClock>();
        services.TryAddScoped<TimeProvider>(provider => provider.GetRequiredService<MessageClock>());
    }
}
