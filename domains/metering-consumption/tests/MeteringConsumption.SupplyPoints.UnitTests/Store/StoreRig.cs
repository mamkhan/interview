using BuildingBlocks.Commands;
using BuildingBlocks.Persistence;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// The Store role's application registration with the real command runner, on the two fakes and a clock standing at
// 09:00 UTC on the given day (2026-10-13 unless a test names another), so a test drives the event handler a consumer
// would resolve and reads the rows the fake repository holds.
internal sealed class StoreRig
{
    private readonly ServiceProvider _services;

    public StoreRig(DateOnly? today = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(
            new DateTimeOffset((today ?? new DateOnly(2026, 10, 13)).ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero)));
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton<ISupplyPointRepository>(Repository);
        services.AddCommandRunner();
        services.AddStoreApplication();
        _services = services.BuildServiceProvider();
    }

    public FakeSupplyPointRepository Repository { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public THandler Handler<THandler>() where THandler : notnull =>
        _services.CreateScope().ServiceProvider.GetRequiredService<THandler>();
}
