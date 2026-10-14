using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.Seedwork;
using BuildingBlocks.TestsShared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.IntegrationTests.Scenarios;

// A consumer whose handler rejects every message with a domain rule the same bytes will break again. The message is
// read off the queue's dead-letter sub-queue.
public sealed class RejectedOnAQueueScenario : IAsyncLifetime
{
    private const string Queue = "rejecting";

    private SuiteScope? _scope;
    private IHost? _host;

    public List<string> Attempts { get; } = [];

    public ServiceBusReceivedMessage? DeadLettered { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = _scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenAConsumerRejectsAMessage),
            new SuiteResources { Queues = [new QueueEntity(Queue)] }, cancellationToken);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new ServiceBusClient(scope.Endpoints.ServiceBusConnectionString));
        builder.Services.AddSingleton(Attempts);
        builder.Services.AddSingleton(scope);
        builder.Services.AddScoped<IEventHandler<Unplaceable>, RejectingHandler>();
        builder.Services.AddHostedService<RejectingConsumer>();
        _host = builder.Build();
        await _host.StartAsync(cancellationToken);

        var client = _host.Services.GetRequiredService<ServiceBusClient>();
        await using var sender = client.CreateScopedSender(scope, Queue);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(new Unplaceable("undated")))
        {
            MessageId = "undated",
        }, cancellationToken);

        await using var deadLetters = client.CreateReceiver(scope.Queue(Queue),
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        DeadLettered = await deadLetters.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
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

    public sealed record Unplaceable(string Name);

    public sealed class NoPlaceForIt() : DomainException("the statement carries no date to place it on");

    private sealed class RejectingHandler(List<string> attempts) : IEventHandler<Unplaceable>
    {
        public Task HandleAsync(Unplaceable message, CancellationToken cancellationToken)
        {
            lock (attempts)
            {
                attempts.Add(message.Name);
            }

            throw new NoPlaceForIt();
        }
    }

    private sealed class RejectingConsumer(
        ServiceBusClient client, IServiceScopeFactory scopeFactory, ILogger<RejectingConsumer> logger, SuiteScope scope)
        : EventConsumer<Unplaceable>(client, scope.Queue(Queue), scopeFactory, logger);
}
