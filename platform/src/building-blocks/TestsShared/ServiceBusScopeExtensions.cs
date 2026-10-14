using Azure.Messaging.ServiceBus;

namespace BuildingBlocks.TestsShared;

// A test names the logical topic or queue and the scope supplies the prefix; the raw logical name does
// not exist on the emulator.
public static class ServiceBusScopeExtensions
{
    public static ServiceBusSender CreateScopedSender(this ServiceBusClient client, SuiteScope scope,
        string topicOrQueue) =>
        client.CreateSender(scope.Topic(topicOrQueue));

    public static ServiceBusReceiver CreateScopedReceiver(this ServiceBusClient client, SuiteScope scope,
        string topic, string subscription) =>
        client.CreateReceiver(scope.Topic(topic), SuiteScope.Subscription(subscription));

    public static Task<ServiceBusSessionReceiver> CreateScopedSessionReceiverAsync(this ServiceBusClient client,
        SuiteScope scope, string topic, string subscription, string sessionId,
        CancellationToken cancellationToken = default) =>
        client.AcceptSessionAsync(scope.Topic(topic), SuiteScope.Subscription(subscription), sessionId,
            cancellationToken: cancellationToken);
}
