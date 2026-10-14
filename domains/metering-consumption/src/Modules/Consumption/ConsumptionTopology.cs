namespace MeteringConsumption.Consumption;

// The entities only this module consumes from, identical in every environment. A subscription is named
// `{domain}.{feature}-{event}` after the event handler that reads it, so the portal shows which feature reads
// which event. Editing a name here renames nothing deployed: a changed name is a new, empty entity.
public static class ConsumptionTopology
{
    // Every event this module publishes names it as its publisher, whichever slice raises it.
    public const string PublishingContext = "metering-consumption.consumption";

    public static class Subscriptions
    {
        public const string RecordIntervalReadingReceived = "metr.record-interval-reading-received";

        public const string ProfileMeterConfigurationReceived = "metr.profile-meter-configuration-received";

        public const string ProfileUsageFactorsReceived = "metr.profile-usage-factors-received";

        public const string ProfileTariffResolved = "metr.profile-tariff-resolved";

        public const string ForecastConsumptionRecorded = "metr.forecast-consumption-recorded";
    }
}
