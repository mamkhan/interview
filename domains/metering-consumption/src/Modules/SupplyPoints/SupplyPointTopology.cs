namespace MeteringConsumption.SupplyPoints;

// The entities only this module consumes from, identical in every environment. A subscription is named for its event
// alone, the same name as its topic, which Azure allows because a subscription lives inside its topic. That holds while
// SupplyPoints is the topic's only reader: a second module reading it names its subscription `{domain}.{feature}-{event}`.
// Editing a name here renames nothing deployed: a changed name is a new, empty entity.
public static class SupplyPointTopology
{
    // Every event this module publishes names it as its publisher, whichever slice raises it.
    public const string PublishingContext = "metering-consumption.supply-point";

    public static class Subscriptions
    {
        public const string MeterConfigurationChangeReceived = "metr.meter-configuration-change-received";

        // The topic is the platform's, so this one name takes this domain's code instead of its topic's.
        public const string SupplyPointLoadedFromV1 = "metr.supply-point-loaded-from-v1";
    }
}
