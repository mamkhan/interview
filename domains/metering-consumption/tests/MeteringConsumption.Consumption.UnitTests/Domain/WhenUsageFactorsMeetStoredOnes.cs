using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile already holds the estimated 24H factor, 3910.0 effective 2026-09-21. Each factor keeps its
// latest version by its own effective date, whatever order the files arrive in; on the same date, the one
// applied last wins.
public class WhenUsageFactorsMeetStoredOnes
{
    private static readonly UsageFactorSlot Estimated24H = new("24H", UsageFactorKind.Estimated);

    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");

    public WhenUsageFactorsMeetStoredOnes()
    {
        _profile.Apply([new UsageFactorStatementBuilder().Build()]);
    }

    [Fact]
    public void KeepsTheStoredFactorWhenTheNewOneTakesEffectEarlier()
    {
        _profile.Apply([new UsageFactorStatementBuilder().Estimated(4100.0m).EffectiveFrom(new DateOnly(2026, 9, 1)).Build()]);

        _profile.UsageFactors[Estimated24H].Value.Should().Be(3910.0m);
    }

    [Fact]
    public void ReportsNoChangeForARepeatOfTheStoredFactor() =>
        _profile.Apply([new UsageFactorStatementBuilder().Build()]).Should().BeFalse();

    [Fact]
    public void ReportsNoChangeWhenEveryFactorTakesEffectEarlier() =>
        _profile.Apply([new UsageFactorStatementBuilder().EffectiveFrom(new DateOnly(2026, 9, 1)).Build()])
            .Should().BeFalse();

    [Fact]
    public void ReplacesTheStoredFactorWhenTheNewOneTakesEffectLater()
    {
        _profile.Apply([new UsageFactorStatementBuilder().Estimated(4100.0m).EffectiveFrom(new DateOnly(2026, 10, 1)).Build()]);

        _profile.UsageFactors[Estimated24H].Value.Should().Be(4100.0m);
    }

    [Fact]
    public void ReplacesTheStoredFactorWithACorrectionForTheSameDate()
    {
        _profile.Apply([new UsageFactorStatementBuilder()
            .Estimated(3925.12345678m)
            .FromSource("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19")
            .Build()]);

        _profile.UsageFactors[Estimated24H].Value.Should().Be(3925.12345678m);
        _profile.UsageFactors[Estimated24H].Source.Should().Be("9f3c0c2d6e1b4a7f8e5d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a19");
    }

    [Fact]
    public void KeepsAFactorTheMessageDoesNotMention()
    {
        _profile.Apply([new UsageFactorStatementBuilder().ForTimeslot("00N").Estimated(1250.5m).Build()]);

        _profile.UsageFactors[Estimated24H].Value.Should().Be(3910.0m);
        _profile.UsageFactors[new UsageFactorSlot("00N", UsageFactorKind.Estimated)].Value.Should().Be(1250.5m);
    }

    [Fact]
    public void JudgesEachFactorOnItsOwnDate()
    {
        _profile.Apply(
        [
            new UsageFactorStatementBuilder().Estimated(4100.0m).EffectiveFrom(new DateOnly(2026, 9, 1)).Build(),
            new UsageFactorStatementBuilder().ForTimeslot("00D").Estimated(2600.0m).EffectiveFrom(new DateOnly(2026, 9, 1)).Build(),
        ]);

        _profile.UsageFactors[Estimated24H].Value.Should().Be(3910.0m);
        _profile.UsageFactors[new UsageFactorSlot("00D", UsageFactorKind.Estimated)].Value.Should().Be(2600.0m);
    }

    [Fact]
    public void LeavesTheConfigurationAsItWas()
    {
        _profile.Apply([new UsageFactorStatementBuilder().EffectiveFrom(new DateOnly(2026, 10, 1)).Build()]);

        _profile.Configuration.Should().BeNull();
    }
}
