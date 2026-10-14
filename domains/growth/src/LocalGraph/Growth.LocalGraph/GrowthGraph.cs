using Aspire.Hosting.Azure;
using Growth.Contracts;
using Platform.LocalGraph;

namespace Growth.LocalGraph;

// What this domain adds to the shared fixtures locally: its topics, named from the contracts topology. A
// consuming domain's fixtures take this record and add their own subscriptions to these topics.
public sealed record GrowthFixtures(
    PlatformFixtures Platform,
    IResourceBuilder<AzureServiceBusTopicResource> TariffResolved);

public static class GrowthGraph
{
    // Local only: the Service Bus emulator refuses to start with a topic that has no subscription, and Growth
    // consumes none of its own events, so this one also lets a local run read what was published.
    public const string ObserveTariffResolvedSubscription = "grow.observe-tariff-resolved";

    public static GrowthFixtures AddGrowthFixtures(this IDistributedApplicationBuilder builder, PlatformFixtures platform)
    {
        var tariffResolved = platform.ServiceBus.AddServiceBusTopic("tariff-resolved", GrowthTopology.Topics.TariffResolved);
        tariffResolved.AddServiceBusSubscription("observe-tariff-resolved", ObserveTariffResolvedSubscription);
        return new GrowthFixtures(platform, tariffResolved);
    }
}
