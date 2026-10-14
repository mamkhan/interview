using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.IntegrationTests.Scenarios;

public sealed class ConsumedFromAQueueScenario : IAsyncLifetime
{
    private const string Queue = "inbound";

    private SuiteScope? _scope;
    private IHost? _host;

    public List<string> Delivered { get; } = [];

    public ServiceBusReceivedMessage? LeftOnTheQueue { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenConsumingFromAQueue),
            new SuiteResources { Queues = [new QueueEntity(Queue)] }, cancellationToken);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString));
        builder.Services.AddSingleton(Delivered);
        builder.Services.AddSingleton(scope);
        builder.Services.AddScoped<IEventHandler<Enqueued>, RecordingHandler>();
        builder.Services.AddHostedService<QueueConsumer>();
        _host = builder.Build();
        await _host.StartAsync(cancellationToken);

        var client = _host.Services.GetRequiredService<ServiceBusClient>();
        await using var sender = client.CreateScopedSender(scope, Queue);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(new Enqueued("only")))
        {
            MessageId = "only",
        }, cancellationToken);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (Delivered.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, cancellationToken);
        }

        // The consumer completes the message after the handler returns, so the queue is read from its first
        // message until that message has left it.
        await using var receiver = client.CreateReceiver(scope.Queue(Queue));
        do
        {
            LeftOnTheQueue = await receiver.PeekMessageAsync(fromSequenceNumber: 0, cancellationToken);
            if (LeftOnTheQueue is not null)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
        while (LeftOnTheQueue is not null && DateTimeOffset.UtcNow < deadline);
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

    public sealed record Enqueued(string Name);

    private sealed class RecordingHandler(List<string> delivered) : IEventHandler<Enqueued>
    {
        public Task HandleAsync(Enqueued message, CancellationToken cancellationToken)
        {
            lock (delivered)
            {
                delivered.Add(message.Name);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class QueueConsumer(
        ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<QueueConsumer> logger, SuiteScope scope)
        : EventConsumer<Enqueued>(client, scope.Queue(Queue), scopeFactory, logger,
            maxAutoLockRenewal: TimeSpan.FromMinutes(2));
}
