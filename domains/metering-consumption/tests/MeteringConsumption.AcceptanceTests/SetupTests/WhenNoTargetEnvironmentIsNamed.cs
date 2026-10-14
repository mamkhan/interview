using AwesomeAssertions;

namespace MeteringConsumption.AcceptanceTests.SetupTests;

// The literals are the local graph's ports and password and Azurite's published development account.
public sealed class WhenNoTargetEnvironmentIsNamed
{
    private readonly TargetEnvironment _environment = TargetEnvironment.Resolve(_ => null);

    [Fact]
    public void TargetsTheLocalSqlServerOnItsPinnedPort() =>
        _environment.SqlConnectionString.Should().Be(
            "Server=127.0.0.1,14330;Initial Catalog=metering-consumption;User ID=sa;Password=LocalDev!Passw0rd;TrustServerCertificate=True;");

    [Fact]
    public void TargetsAzuriteOnItsPinnedBlobPort() =>
        _environment.Blobs().Uri.Should().Be(new Uri("http://127.0.0.1:10010/devstoreaccount1"));

    [Fact]
    public async Task TargetsTheLocalServiceBusEmulator()
    {
        await using var bus = _environment.ServiceBus();

        bus.FullyQualifiedNamespace.Should().Be("127.0.0.1");
    }

    [Fact]
    public void TargetsTheLocalMarketMessageHost() =>
        _environment.MarketMsgUrl.Should().Be(new Uri("http://localhost:5320/"));

    [Fact]
    public void TargetsTheLocalReceiverHost() =>
        _environment.ReceiverUrl.Should().Be(new Uri("http://localhost:5330/"));
}
