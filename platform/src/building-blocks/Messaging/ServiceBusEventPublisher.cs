using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace BuildingBlocks.Messaging;

public sealed class ServiceBusEventPublisher(ServiceBusClient client, EventTopicMap topics, EntityNames names) : IEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public Task PublishAsync<TEvent>(TEvent message, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class =>
        SendAsync(topics.TopicFor(typeof(TEvent)), typeof(TEvent).FullName!,
            new BinaryData(JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions)), metadata,
            cancellationToken);

    public Task PublishAsync(object message, Type eventType, EventMetadata metadata, CancellationToken cancellationToken) =>
        SendAsync(topics.TopicFor(eventType), eventType.FullName!,
            new BinaryData(JsonSerializer.SerializeToUtf8Bytes(message, eventType, SerializerOptions)), metadata,
            cancellationToken);

    public Task PublishRawAsync(string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken) =>
        SendAsync(topics.TopicFor(eventTypeName), eventTypeName, payload, metadata, cancellationToken);

    private Task SendAsync(string topic, string eventTypeName, BinaryData payload, EventMetadata metadata,
        CancellationToken cancellationToken)
    {
        var sender = _senders.GetOrAdd(topic, logical => client.CreateSender(names.Resolve(logical)));
        var message = new ServiceBusMessage(payload)
        {
            MessageId = metadata.MessageId,
            CorrelationId = metadata.CorrelationId,
            ContentType = "application/json",
            Subject = eventTypeName,
        };
        if (metadata.SessionId is not null)
        {
            message.SessionId = metadata.SessionId;
        }

        if (metadata.ApplicationProperties is not null)
        {
            foreach (var (key, value) in metadata.ApplicationProperties)
            {
                message.ApplicationProperties[key] = value;
            }
        }

        return sender.SendMessageAsync(message, cancellationToken);
    }
}
