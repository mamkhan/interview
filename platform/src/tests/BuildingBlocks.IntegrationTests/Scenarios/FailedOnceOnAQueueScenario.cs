using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.IntegrationTests.Scenarios;

// A consumer whose handler fails the first time it sees a message, as a store that is briefly unreachable does, and
// succeeds the second time. The queue's lock is a minute, so a redelivery within seconds is the abandon's doing.
public sealed class FailedOnceOnAQueueScenario : IAsyncLifetime
{
    private const string Queue = "flaky";

    private SuiteScope? _scope;
    private IHost? _host;

    public List<string> Attempts { get; } = [];

    public TimeSpan UntilHandled { get; private set; }

    public ServiceBusReceivedMessage? LeftOnTheQueue { get; private set; }

    public ServiceBusReceivedMessage? DeadLettered { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAConsumerFailsTransiently),
            new SuiteResources { Queues = [new QueueEntity(Queue)] }, cancellationToken);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString));
        builder.Services.AddSingleton(Attempts);
        builder.Services.AddSingleton(scope);
        builder.Services.AddScoped<IEventHandler<Flaky>, FailingOnceHandler>();
        builder.Services.AddHostedService<FlakyConsumer>();
        _host = builder.Build();
        await _host.StartAsync(cancellationToken);

        var client = _host.Services.GetRequiredService<ServiceBusClient>();
        await using var sender = client.CreateScopedSender(scope, Queue);
        var sent = DateTimeOffset.UtcNow;
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(new Flaky("flaky")))
        {
            MessageId = "flaky",
        }, cancellationToken);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (Attempts.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, cancellationToken);
        }

        UntilHandled = DateTimeOffset.UtcNow - sent;

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

        await using var deadLetters = client.CreateReceiver(scope.Queue(Queue),
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        DeadLettered = await deadLetters.PeekMessageAsync(fromSequenceNumber: 0, cancellationToken);
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

    public sealed record Flaky(string Name);

    private sealed class StoreUnreachable() : Exception("the store did not answer");

    private sealed class FailingOnceHandler(List<string> attempts) : IEventHandler<Flaky>
    {
        public Task HandleAsync(Flaky message, CancellationToken cancellationToken)
        {
            lock (attempts)
            {
                attempts.Add(message.Name);
                if (attempts.Count == 1)
                {
                    throw new StoreUnreachable();
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FlakyConsumer(
        ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<FlakyConsumer> logger, SuiteScope scope)
        : EventConsumer<Flaky>(client, scope.Queue(Queue), scopeFactory, logger);
}
