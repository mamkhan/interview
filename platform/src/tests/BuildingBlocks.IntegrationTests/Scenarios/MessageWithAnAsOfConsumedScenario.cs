using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.IntegrationTests.Scenarios;

public sealed class MessageWithAnAsOfConsumedScenario : IAsyncLifetime
{
    private static readonly DateTimeOffset ReplayedAt = new(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

    private SuiteScope? _scope;
    private IHost? _host;

    public Dictionary<string, DateTimeOffset> Observed { get; } = [];

    public DateTimeOffset Before { get; private set; }

    public DateTimeOffset After { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenConsumingAMessageThatCarriesAnAsOf),
            new SuiteResources { Topics = [new BusEntity(Suite.EventsTopic, Suite.TestReader)] }, cancellationToken);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.TryAddTimeProvider();
        builder.Services.AddSingleton(new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString));
        builder.Services.AddSingleton(Observed);
        builder.Services.AddSingleton(scope);
        builder.Services.AddScoped<IEventHandler<Stamped>, ClockReadingHandler>();
        builder.Services.AddHostedService<StampedConsumer>();
        _host = builder.Build();

        Before = DateTimeOffset.UtcNow;
        await _host.StartAsync(cancellationToken);

        var client = _host.Services.GetRequiredService<ServiceBusClient>();
        var publisher = new ServiceBusEventPublisher(client,
            new EventTopicMap().Register<Stamped>(scope.Topic(Suite.EventsTopic)), Suite.UnprefixedNames);
        await publisher.PublishAsync(new Stamped("replayed", ReplayedAt),
            new EventMetadata { MessageId = "replayed", CorrelationId = "asof" }, cancellationToken);
        await publisher.PublishAsync(new Stamped("live", null),
            new EventMetadata { MessageId = "live", CorrelationId = "asof" }, cancellationToken);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (Observed.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, cancellationToken);
        }

        After = DateTimeOffset.UtcNow;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }

    public sealed record Stamped(string Name, DateTimeOffset? AsOf);

    private sealed class ClockReadingHandler(TimeProvider time, Dictionary<string, DateTimeOffset> observed)
        : IEventHandler<Stamped>
    {
        public Task HandleAsync(Stamped message, CancellationToken cancellationToken)
        {
            lock (observed)
            {
                observed[message.Name] = time.GetUtcNow();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StampedConsumer(
        ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<StampedConsumer> logger, SuiteScope scope)
        : EventConsumer<Stamped>(client, scope.Topic(Suite.EventsTopic), Suite.TestReader, scopeFactory, logger);
}
