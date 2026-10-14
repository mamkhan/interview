namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// This project's naming root on the shared stack: every class's database and bus entities sit under it.
// test-reader is the subscription a feature test reads results off, beside the production ones.
internal static class Suite
{
    public const string Id = "mm";

    public const string TestReader = "test-reader";
}
