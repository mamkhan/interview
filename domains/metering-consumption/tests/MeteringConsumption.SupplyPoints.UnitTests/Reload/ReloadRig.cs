using BuildingBlocks.Commands;
using BuildingBlocks.Persistence;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.UnitTests.Store;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.SupplyPoints.UnitTests.Reload;

// The Reload role's application registration beside Store's, with the real command runner, on one fake repository and a
// clock standing at 2026-10-13 09:00 UTC, so a test can store a market message and then load the same supply point from
// V1, and read the rows that remain.
internal sealed class ReloadRig
{
    private readonly ServiceProvider _services;

    public ReloadRig()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton<ISupplyPointRepository>(Repository);
        services.AddSingleton<ISupplyPointReloadRepository>(Repository);
        services.AddCommandRunner();
        services.AddStoreApplication();
        services.AddReloadApplication();
        _services = services.BuildServiceProvider();
    }

    public FakeSupplyPointRepository Repository { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public THandler Handler<THandler>() where THandler : notnull =>
        _services.CreateScope().ServiceProvider.GetRequiredService<THandler>();
}
