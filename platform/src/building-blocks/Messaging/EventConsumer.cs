using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Seedwork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging;

/// <summary>The Service Bus adapter for a slice's <see cref="IEventHandler{TEvent}"/>, over a topic
/// subscription or a queue: at-least-once, acknowledge last, deterministic failures dead-lettered on first
/// delivery, everything else abandoned for redelivery. Each delivery runs in its own DI scope.</summary>
public abstract class EventConsumer<TEvent> : BackgroundService
    where TEvent : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // The envelope lives in a domain's contracts, which this library cannot reference, so the AsOf field
    // is found by name and type; an event without one runs on the system clock.
    private static readonly PropertyInfo? AsOfProperty =
        typeof(TEvent).GetProperty("AsOf", typeof(DateTimeOffset?));

    private readonly ServiceBusClient _client;
    private readonly string _entity;
    private readonly string? _subscription;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly bool _sessionEnabled;
    private readonly int _maxConcurrency;
    private readonly TimeSpan? _maxAutoLockRenewal;
    private readonly TimeSpan? _sessionIdleTimeout;

    protected EventConsumer(
        ServiceBusClient client,
        string topic,
        string subscription,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        bool sessionEnabled = false,
        int maxConcurrency = 1,
        TimeSpan? maxAutoLockRenewal = null,
        TimeSpan? sessionIdleTimeout = null)
    {
        _client = client;
        _entity = topic;
        _subscription = subscription;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _sessionEnabled = sessionEnabled;
        _maxConcurrency = maxConcurrency;
        _maxAutoLockRenewal = maxAutoLockRenewal;
        _sessionIdleTimeout = sessionIdleTimeout;
    }

    protected EventConsumer(
        ServiceBusClient client,
        string queue,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        int maxConcurrency = 1,
        TimeSpan? maxAutoLockRenewal = null)
    {
        _client = client;
        _entity = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxConcurrency = maxConcurrency;
        _maxAutoLockRenewal = maxAutoLockRenewal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_sessionEnabled)
        {
            var options = new ServiceBusSessionProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentSessions = _maxConcurrency,
                MaxConcurrentCallsPerSession = 1,
            };
            if (_maxAutoLockRenewal is { } renewal)
            {
                options.MaxAutoLockRenewalDuration = renewal;
            }

            // Without it the processor waits its full try timeout, a minute, for another message on a session
            // before it moves on, so a subscription whose sessions hold one message each crawls.
            if (_sessionIdleTimeout is { } idle)
            {
                options.SessionIdleTimeout = idle;
            }

            var processor = _client.CreateSessionProcessor(_entity, _subscription!, options);
            processor.ProcessMessageAsync += args => ProcessAsync(args.Message,
                (message, token) => args.CompleteMessageAsync(message, token),
                (message, reason, description, token) => args.DeadLetterMessageAsync(message, reason, description, token),
                args.CancellationToken);
            processor.ProcessErrorAsync += OnErrorAsync;
            await processor.StartProcessingAsync(stoppingToken);
            await WaitForShutdownAsync(processor.StopProcessingAsync, stoppingToken);
        }
        else
        {
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = _maxConcurrency,
            };
            if (_maxAutoLockRenewal is { } renewal)
            {
                options.MaxAutoLockRenewalDuration = renewal;
            }

            var processor = _subscription is null
                ? _client.CreateProcessor(_entity, options)
                : _client.CreateProcessor(_entity, _subscription, options);
            processor.ProcessMessageAsync += args => ProcessAsync(args.Message,
                (message, token) => args.CompleteMessageAsync(message, token),
                (message, reason, description, token) => args.DeadLetterMessageAsync(message, reason, description, token),
                args.CancellationToken);
            processor.ProcessErrorAsync += OnErrorAsync;
            await processor.StartProcessingAsync(stoppingToken);
            await WaitForShutdownAsync(processor.StopProcessingAsync, stoppingToken);
        }
    }

    private async Task ProcessAsync(
        ServiceBusReceivedMessage message,
        Func<ServiceBusReceivedMessage, CancellationToken, Task> complete,
        Func<ServiceBusReceivedMessage, string, string?, CancellationToken, Task> deadLetter,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["MessageId"] = message.MessageId,
            ["EventType"] = typeof(TEvent).Name,
        });

        TEvent? @event;
        try
        {
            @event = message.Body.ToObjectFromJson<TEvent>(SerializerOptions);
        }
        catch (JsonException exception)
        {
            // Deterministic: redelivery cannot fix a body that does not deserialise.
            _logger.LogError(exception, "Dead-lettering undeserialisable message {MessageId}", message.MessageId);
            await deadLetter(message, "deserialisation-failed", exception.Message, cancellationToken);
            return;
        }

        if (@event is null)
        {
            await deadLetter(message, "empty-body", null, cancellationToken);
            return;
        }

        // A deterministic failure (a validation or invariant the same bytes will fail again) dead-letters
        // on first delivery. Anything else propagates, the message is abandoned, and Service Bus
        // redelivery is the retry, bounded by the entity's MaxDeliveryCount into the dead-letter queue.
        try
        {
            await using var deliveryScope = _scopeFactory.CreateAsyncScope();
            StampClock(deliveryScope.ServiceProvider, @event);
            await ResolveHandler(deliveryScope.ServiceProvider).HandleAsync(@event, cancellationToken);
        }
        catch (Exception exception) when (exception is DomainException or CommandValidationException)
        {
            _logger.LogError(exception, "Dead-lettering message {MessageId}: {Reason}", message.MessageId, exception.GetType().Name);
            await deadLetter(message, exception.GetType().Name, exception.Message, cancellationToken);
            return;
        }

        await complete(message, cancellationToken);
    }

    // Two slices in one host may subscribe to the same event; a consumer that must reach a particular one
    // derives from EventConsumer<TEvent, THandler>.
    protected virtual IEventHandler<TEvent> ResolveHandler(IServiceProvider deliveryServices) =>
        deliveryServices.GetRequiredService<IEventHandler<TEvent>>();

    private static void StampClock(IServiceProvider deliveryServices, TEvent @event)
    {
        if (AsOfProperty?.GetValue(@event) is DateTimeOffset asOf
            && deliveryServices.GetService<MessageClock>() is { } clock)
        {
            clock.AsOf = asOf;
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus processor error on {Entity} ({Source})",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    private static async Task WaitForShutdownAsync(Func<CancellationToken, Task> stop, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown: stop pulling new messages; in-flight handlers finish under their own token.
        }

        await stop(CancellationToken.None);
    }
}

/// <summary>A consumer bound to one handler type, for a host where several slices subscribe to the same
/// event. The handler is registered scoped under its own type.</summary>
public abstract class EventConsumer<TEvent, THandler> : EventConsumer<TEvent>
    where TEvent : class
    where THandler : IEventHandler<TEvent>
{
    protected EventConsumer(
        ServiceBusClient client,
        string topic,
        string subscription,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        bool sessionEnabled = false,
        int maxConcurrency = 1,
        TimeSpan? maxAutoLockRenewal = null,
        TimeSpan? sessionIdleTimeout = null)
        : base(client, topic, subscription, scopeFactory, logger, sessionEnabled, maxConcurrency, maxAutoLockRenewal,
            sessionIdleTimeout)
    {
    }

    protected EventConsumer(
        ServiceBusClient client,
        string queue,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        int maxConcurrency = 1,
        TimeSpan? maxAutoLockRenewal = null)
        : base(client, queue, scopeFactory, logger, maxConcurrency, maxAutoLockRenewal)
    {
    }

    protected override IEventHandler<TEvent> ResolveHandler(IServiceProvider deliveryServices) =>
        deliveryServices.GetRequiredService<THandler>();
}

