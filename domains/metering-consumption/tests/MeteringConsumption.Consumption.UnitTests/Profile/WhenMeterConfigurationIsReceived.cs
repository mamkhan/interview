using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// The agreed MeterConfigurationReceived example reaches a supply point that has no profile yet.
public class WhenMeterConfigurationIsReceived
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 5, TimeSpan.Zero);

    private readonly FakeProfileRepository _repository = new();

    public WhenMeterConfigurationIsReceived()
    {
        new ProfileMeterConfigurationReceivedEventHandler(_repository, new FixedTimeProvider(Now),
                NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance)
            .HandleAsync(new MeterConfigurationReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresTheCodesTheMessageStates()
    {
        var configuration = _repository.Stored("10012345678")!.Configuration!;
        configuration.MeterConfigurationCode.Should().Be("MCC01");
        configuration.LoadProfileCode.Should().Be("01");
        configuration.DuosGroup.Should().Be("DG1");
        configuration.MeterPointStatusCode.Should().Be("E");
    }

    [Fact]
    public void KeysTheProfileOnTheMprnAndTheMarket()
    {
        _repository.Stored("10012345678")!.SupplyPointId.Should().Be("10012345678");
        _repository.Stored("10012345678")!.Market.Should().Be("roi-esb");
    }

    [Fact]
    public void RecordsTheEffectiveDateAndTheSealedFile()
    {
        var configuration = _repository.Stored("10012345678")!.Configuration!;
        configuration.EffectiveFrom.Should().Be(new DateOnly(2026, 9, 21));
        configuration.Source.Should().Be("801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25");
        configuration.MessageType.Should().Be("332");
    }

    [Fact]
    public void StampsTheUpdateTimeFromTheClock() => _repository.UpdatedAt.Should().Be(Now);

    [Fact]
    public void SavesTheConfigurationAndNothingElse() => _repository.Saves.Should().Equal("configuration");
}

// A newer configuration is already stored, and a message effective earlier arrives late, or the stored
// message arrives again.
public class WhenAnOlderOrRepeatedMeterConfigurationIsReceived
{
    private readonly FakeProfileRepository _repository = new();
    private readonly ProfileMeterConfigurationReceivedEventHandler _handler;

    public WhenAnOlderOrRepeatedMeterConfigurationIsReceived()
    {
        var stored = SupplyPointProfile.New("10012345678", "roi-esb");
        stored.Apply(new MeterConfigurationStatementBuilder().Build());
        _repository.Seed(stored);
        _handler = new ProfileMeterConfigurationReceivedEventHandler(_repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 5, TimeSpan.Zero)),
            NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance);
    }

    [Fact]
    public async Task SavesNothingForAnEarlierEffectiveDate()
    {
        await _handler.HandleAsync(new MeterConfigurationReceivedBuilder()
            .WithMeterConfigurationCode("MCC02").EffectiveFrom(new DateOnly(2026, 9, 1)).Build(), CancellationToken.None);

        _repository.Saves.Should().BeEmpty();
        _repository.Stored("10012345678")!.Configuration!.MeterConfigurationCode.Should().Be("MCC01");
    }

    [Fact]
    public async Task SavesNothingForTheSameMessageAgain()
    {
        await _handler.HandleAsync(new MeterConfigurationReceivedBuilder().Build(), CancellationToken.None);

        _repository.Saves.Should().BeEmpty();
    }
}

// A 320 states a configuration with no effective date of its own, only the change-of-supplier reading's date, so the
// profile dates it by the reading.
public class WhenAMeterConfigurationFromAChangeOfSupplierReadingIsReceived
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 5, TimeSpan.Zero);

    private readonly FakeProfileRepository _repository = new();

    public WhenAMeterConfigurationFromAChangeOfSupplierReadingIsReceived()
    {
        new ProfileMeterConfigurationReceivedEventHandler(_repository, new FixedTimeProvider(Now),
                NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance)
            .HandleAsync(new MeterConfigurationReceivedBuilder().ReadOn(new DateOnly(2026, 6, 24)).FromFile("320file", "320").Build(),
                         CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void RecordsTheReadDateAsTheEffectiveDate() =>
        _repository.Stored("10012345678")!.Configuration!.EffectiveFrom.Should().Be(new DateOnly(2026, 6, 24));

    [Fact]
    public void RecordsTheMessageTypeItCameFrom() =>
        _repository.Stored("10012345678")!.Configuration!.MessageType.Should().Be("320");
}

// A configuration with neither date cannot be ordered against the profile's others, so it is refused rather than
// stored under a date it never stated.
public class WhenAMeterConfigurationCarriesNoDate
{
    private readonly FakeProfileRepository _repository = new();

    [Fact]
    public async Task RefusesTheStatementAndSavesNothing()
    {
        var handler = new ProfileMeterConfigurationReceivedEventHandler(_repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch),
                                                                        NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance);

        var handle = () => handler.HandleAsync(new MeterConfigurationReceivedBuilder().WithNoDates().Build(), CancellationToken.None);

        await handle.Should().ThrowAsync<ProfileStatementRejectedException>();
        _repository.Saves.Should().BeEmpty();
    }
}

// A market message leaves the smart meter flag empty, as every market message does today, for a supply point that
// already holds V1's flag: the flag is not touched and only the configuration is saved.
public class WhenAMeterConfigurationLeavesTheSmartMeterFlagEmpty
{
    private readonly FakeProfileRepository _repository = new();

    public WhenAMeterConfigurationLeavesTheSmartMeterFlagEmpty()
    {
        var stored = SupplyPointProfile.New("10012345678", "roi-esb");
        stored.Apply(new SmartMeterFlagStatementBuilder().Build());
        _repository.Seed(stored);

        new ProfileMeterConfigurationReceivedEventHandler(_repository,
                new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)),
                NullLogger<ProfileMeterConfigurationReceivedEventHandler>.Instance)
            .HandleAsync(new MeterConfigurationReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void KeepsTheStoredFlag()
    {
        var flag = _repository.Stored("10012345678")!.SmartMeterFlag!;
        flag.IsNsmp.Should().BeTrue();
        flag.Source.Should().Be("v1-reader:2026-10-07:1");
    }

    [Fact]
    public void SavesTheConfigurationAndNothingElse() => _repository.Saves.Should().Equal("configuration");
}
