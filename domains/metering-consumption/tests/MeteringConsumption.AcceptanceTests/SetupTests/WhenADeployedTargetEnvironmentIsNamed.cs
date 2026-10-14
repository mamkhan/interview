using AwesomeAssertions;

namespace MeteringConsumption.AcceptanceTests.SetupTests;

// The literals are the dev resources as Terragrunt applies them.
public sealed class WhenADeployedTargetEnvironmentIsNamed
{
    private static readonly Dictionary<string, string> Variables = new()
    {
        ["ACCEPTANCE_ENVIRONMENT"] = "dev",
        ["ACCEPTANCE_METR_MARKETMSG_URL"] = "https://acmdevmetrcamarketmsg.example.northeurope.azurecontainerapps.io",
        ["ACCEPTANCE_METR_RECEIVER_URL"] = "https://acmdevmetrcareceiver.example.northeurope.azurecontainerapps.io",
    };

    private readonly TargetEnvironment _environment = TargetEnvironment.Resolve(Variables.GetValueOrDefault);

    [Fact]
    public void ReachesTheSqlServerByItsConventionName() =>
        _environment.SqlConnectionString.Should().Be(
            "Server=tcp:acmdevmetrsqlmain.database.windows.net,1433;Initial Catalog=metering-consumption;Authentication=Active Directory Default;Encrypt=True;");

    [Fact]
    public void ReachesTheArchiveAccountByItsConventionName() =>
        _environment.Blobs().Uri.Should().Be(new Uri("https://acmdevmetrstarchive.blob.core.windows.net/"));

    [Fact]
    public async Task ReachesThePlatformServiceBusByItsConventionName()
    {
        await using var bus = _environment.ServiceBus();

        bus.FullyQualifiedNamespace.Should().Be("acmdevplatsbnsshared.servicebus.windows.net");
    }

    [Fact]
    public void TakesTheMarketMessageHostFromItsVariable() =>
        _environment.MarketMsgUrl.Should().Be(new Uri("https://acmdevmetrcamarketmsg.example.northeurope.azurecontainerapps.io/"));

    [Fact]
    public void TakesTheReceiverHostFromItsVariable() =>
        _environment.ReceiverUrl.Should().Be(new Uri("https://acmdevmetrcareceiver.example.northeurope.azurecontainerapps.io/"));
}
