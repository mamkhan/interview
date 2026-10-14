using BuildingBlocks.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Persistence;

public static class UnitOfWorkRegistration
{
    /// <summary>One scoped unit of work per message or request on the module's database.</summary>
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutboxWriter, DapperOutboxWriter>();
        services.TryAddScoped<IUnitOfWork>(sp => new UnitOfWork(connectionString, sp,
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IOutboxWriter>()));
        return services;
    }
}
