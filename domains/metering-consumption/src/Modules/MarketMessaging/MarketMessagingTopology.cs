using MeteringConsumption.Contracts;

namespace MeteringConsumption.MarketMessaging;

// The entities only this module publishes to and consumes from, identical in every environment. A
// subscription or queue is named `{domain}.{feature}-{event}` after the event handler that reads it, so the
// portal shows which feature reads which event. Editing a name here renames nothing deployed: a changed name
// is a new, empty entity.
public static class MarketMessagingTopology
{
    // Every event this module publishes names it as its publisher, whichever slice raises it.
    public const string PublishingContext = "metering-consumption.market-messaging";

    public static class Topics
    {
        public const string MarketMessageReceived = "metr.market-message-received";

        public const string MarketMessageProcessed = "metr.market-message-processed";

        public const string MarketMessageFailed = "metr.market-message-failed";
    }

    public static class Subscriptions
    {
        public const string InterpretMarketMessageReceived = "metr.interpret-market-message-received";

        public const string AuditMarketMessageReceived = "metr.audit-market-message-received";

        public const string AuditMarketMessageProcessed = "metr.audit-market-message-processed";

        public const string AuditMarketMessageFailed = "metr.audit-market-message-failed";
    }

    public static class Queues
    {
        public const string ReceiveBlobCreated = "metr.receive-blob-created";
    }

    public static class Containers
    {
        public const string EsbDrop = "esb-drop";

        public const string MarketMessageArchive = "market-message-archive";
    }

    public static class Fuels
    {
        public const string Electricity = MeteringConsumptionTopology.Fuels.Electricity;

        public const string Gas = MeteringConsumptionTopology.Fuels.Gas;
    }
}
