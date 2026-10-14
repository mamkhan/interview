using AwesomeAssertions;

namespace MeteringConsumption.AcceptanceTests.SetupTests;

// A developer running one host outside the AppHost points the suite at it.
public sealed class WhenOnlyAHostAddressIsNamed
{
    private static readonly Dictionary<string, string> Variables = new()
    {
        ["ACCEPTANCE_METR_RECEIVER_URL"] = "http://localhost:6330",
    };

    private readonly TargetEnvironment _environment = TargetEnvironment.Resolve(Variables.GetValueOrDefault);

    [Fact]
    public void TakesThatHostFromItsVariable() =>
        _environment.ReceiverUrl.Should().Be(new Uri("http://localhost:6330/"));

    [Fact]
    public void KeepsTheOtherHostOnItsLocalPort() =>
        _environment.MarketMsgUrl.Should().Be(new Uri("http://localhost:5320/"));
}
