using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Commands;

public static class CommandsRegistration
{
    /// <summary>The runner is scoped with the unit of work it drives; a module registers it once per host
    /// role that runs commands, after <c>AddUnitOfWork</c>.</summary>
    public static IServiceCollection AddCommandRunner(this IServiceCollection services)
    {
        services.TryAddScoped<CommandRunner>();
        return services;
    }
}
