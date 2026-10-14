namespace Growth.Contracts;

/// <summary>The names another module needs to consume this domain's published events, identical in every
/// environment. Subscriptions belong to the consuming module, never to the publisher's contracts.</summary>
public static class GrowthTopology
{
    public static class Topics
    {
        public const string TariffResolved = "grow.tariff-resolved";
    }
}
