namespace Platform.Contracts;

/// <summary>The names a domain needs to consume the events platform services publish, identical in every
/// environment. Subscriptions belong to the consuming module, never to the publisher's contracts.</summary>
public static class PlatformTopology
{
    public static class Topics
    {
        public const string SupplyPointLoadedFromV1 = "plat.supply-point-loaded-from-v1";
    }
}
