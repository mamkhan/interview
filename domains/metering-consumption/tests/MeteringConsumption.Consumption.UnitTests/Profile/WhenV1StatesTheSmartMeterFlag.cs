using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// The V1 reader's meter configuration event reaches a supply point that holds the agreed MCC01 configuration: the
// meter is smart, every other optional field is empty, AsOf 2026-10-07T04:00Z, content hash v1-reader:2026-10-07:1.
public class WhenV1StatesTheSmartMeterFlag
{
    private readonly FakeProfileRepository _repository = new();

    public WhenV1StatesTheSmartMeterFlag()
    {
        var stored = SupplyPointProfile.New("10012345678", "roi-esb");
        stored.Apply(new MeterConfigurationStatementBuilder().Build());
        _repository.Seed(stored);

        Handler(_repository).HandleAsync(new MeterConfigurationReceivedBuilder().AsTheV1ReaderStatesIt(isNsmp: true).Build(),
            CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresTheFlagWithWhenItWasReadAndWhereItCameFrom()
    {
        var flag = _repository.Stored("10012345678")!.SmartMeterFlag!;
        flag.IsNsmp.Should().BeTrue();
        flag.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        flag.Source.Should().Be("v1-reader:2026-10-07:1");
    }

    [Fact]
    public void LeavesTheMeterConfigurationAsItWas()
    {
        var configuration = _repository.Stored("10012345678")!.Configuration!;
        configuration.MeterConfigurationCode.Should().Be("MCC01");
        configuration.EffectiveFrom.Should().Be(new DateOnly(2026, 9, 21));
    }

    [Fact]
    public void SavesTheFlagAndNothingElse() => _repository.Saves.Should().Equal("smart meter flag");

    internal static ProfileMeterConfigurationReceivedEventHandler Handler(FakeProfileRepository repository) =>
        new(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero)),
            NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance);
}

// The V1 reader states the flag for a supply point no market message has reached yet.
public class WhenV1StatesTheSmartMeterFlagForANewSupplyPoint
{
    private readonly FakeProfileRepository _repository = new();

    public WhenV1StatesTheSmartMeterFlagForANewSupplyPoint()
    {
        WhenV1StatesTheSmartMeterFlag.Handler(_repository)
            .HandleAsync(new MeterConfigurationReceivedBuilder().AsTheV1ReaderStatesIt(isNsmp: false).Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void CreatesTheProfileWithTheFlagAndNoConfiguration()
    {
        var profile = _repository.Stored("10012345678")!;
        profile.Market.Should().Be("roi-esb");
        profile.SmartMeterFlag!.IsNsmp.Should().BeFalse();
        profile.Configuration.Should().BeNull();
    }
}

// A market message that one day states the flag beside its codes: both groups are applied.
public class WhenAMeterConfigurationStatesCodesAndTheSmartMeterFlag
{
    private readonly FakeProfileRepository _repository = new();

    public WhenAMeterConfigurationStatesCodesAndTheSmartMeterFlag()
    {
        WhenV1StatesTheSmartMeterFlag.Handler(_repository)
            .HandleAsync(new MeterConfigurationReceivedBuilder()
                .StatingTheSmartMeterFlag(isNsmp: true, asOf: new DateTimeOffset(2026, 9, 28, 2, 50, 14, TimeSpan.Zero))
                .Build(), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void AppliesTheCodesAndTheFlag()
    {
        var profile = _repository.Stored("10012345678")!;
        profile.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
        profile.SmartMeterFlag!.IsNsmp.Should().BeTrue();
    }

    [Fact]
    public void SavesBothGroups() => _repository.Saves.Should().Equal("configuration", "smart meter flag");
}

// A flag is ordered by the AsOf of the event that states it; one without it cannot be ordered, so it is refused.
public class WhenASmartMeterFlagArrivesWithoutAsOf
{
    [Fact]
    public async Task RefusesTheEvent()
    {
        var act = () => WhenV1StatesTheSmartMeterFlag.Handler(new FakeProfileRepository())
            .HandleAsync(new MeterConfigurationReceivedBuilder().AsTheV1ReaderStatesIt(isNsmp: true).WithoutAsOf().Build(),
                CancellationToken.None);

        await act.Should().ThrowAsync<ProfileStatementRejectedException>().WithMessage("*AsOf*");
    }
}
