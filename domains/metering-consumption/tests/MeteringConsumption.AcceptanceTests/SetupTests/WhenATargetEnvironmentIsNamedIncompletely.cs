using AwesomeAssertions;

namespace MeteringConsumption.AcceptanceTests.SetupTests;

// Fails before any test touches an environment.
public sealed class WhenATargetEnvironmentIsNamedIncompletely
{
    [Fact]
    public void RejectsAnEnvironmentCodeTheConventionDoesNotKnow()
    {
        var variables = new Dictionary<string, string>
        {
            ["ACCEPTANCE_ENVIRONMENT"] = "qa",
            ["ACCEPTANCE_METR_MARKETMSG_URL"] = "https://marketmsg.example",
            ["ACCEPTANCE_METR_RECEIVER_URL"] = "https://receiver.example",
        };

        var resolve = () => TargetEnvironment.Resolve(variables.GetValueOrDefault);

        resolve.Should().Throw<InvalidOperationException>().WithMessage("*'qa'*local, dev, tst, prd*");
    }

    [Fact]
    public void RejectsADeployedEnvironmentWithoutAHostAddress()
    {
        var variables = new Dictionary<string, string>
        {
            ["ACCEPTANCE_ENVIRONMENT"] = "dev",
            ["ACCEPTANCE_METR_MARKETMSG_URL"] = "https://marketmsg.example",
        };

        var resolve = () => TargetEnvironment.Resolve(variables.GetValueOrDefault);

        resolve.Should().Throw<InvalidOperationException>().WithMessage("*ACCEPTANCE_METR_RECEIVER_URL*acmdevmetrcareceiver*");
    }
}
