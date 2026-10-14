using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// The agreed UsageFactorsReceived example reaches a supply point that has no profile yet.
public class WhenUsageFactorsAreReceived
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 5, TimeSpan.Zero);

    private readonly FakeProfileRepository _repository = new();

    public WhenUsageFactorsAreReceived()
    {
        new ProfileUsageFactorsReceivedEventHandler(_repository, new FixedTimeProvider(Now),
                NullLogger<ProfileUsageFactorsReceivedEventHandler>.Instance)
            .HandleAsync(new UsageFactorsReceivedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresEachFactorWithAllItsDecimals()
    {
        var factors = _repository.Stored("10012345678")!.UsageFactors;
        factors[new UsageFactorSlot("24H", UsageFactorKind.Actual)].Value.Should().Be(3842.5173m);
        factors[new UsageFactorSlot("24H", UsageFactorKind.Estimated)].Value.Should().Be(3910.0m);
    }

    [Fact]
    public void RecordsEachFactorsDateAndTheSealedFile()
    {
        var actual = _repository.Stored("10012345678")!.UsageFactors[new UsageFactorSlot("24H", UsageFactorKind.Actual)];
        actual.EffectiveFrom.Should().Be(new DateOnly(2026, 7, 16));
        actual.Source.Should().Be("801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25");
    }

    [Fact]
    public void StampsTheUpdateTimeFromTheClock() => _repository.UpdatedAt.Should().Be(Now);

    [Fact]
    public void SavesTheUsageFactorsAndNothingElse() => _repository.Saves.Should().Equal("usage factors");
}

// A supply point that already has its configuration and factors receives more usage factor messages.
public class WhenMoreUsageFactorsAreReceived
{
    private readonly FakeProfileRepository _repository = new();
    private readonly ProfileUsageFactorsReceivedEventHandler _handler;

    public WhenMoreUsageFactorsAreReceived()
    {
        var stored = SupplyPointProfile.New("10012345678", "roi-esb");
        stored.Apply(new MeterConfigurationStatementBuilder().Build());
        stored.Apply([new UsageFactorStatementBuilder().Build()]);
        _repository.Seed(stored);
        _handler = new ProfileUsageFactorsReceivedEventHandler(_repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 5, TimeSpan.Zero)),
            NullLogger<ProfileUsageFactorsReceivedEventHandler>.Instance);
    }

    [Fact]
    public async Task KeepsTheConfigurationAsItWas()
    {
        await _handler.HandleAsync(new UsageFactorsReceivedBuilder().Build(), CancellationToken.None);

        _repository.Stored("10012345678")!.Configuration.Should().Be(new MeterConfigurationStatementBuilder().Build());
    }

    [Fact]
    public async Task SavesNothingForAMessageWithNoFactors()
    {
        await _handler.HandleAsync(new UsageFactorsReceivedBuilder().WithNoFactors().Build(), CancellationToken.None);

        _repository.Saves.Should().BeEmpty();
    }

    [Fact]
    public async Task SavesNothingForFactorsThatTakeEffectEarlier()
    {
        await _handler.HandleAsync(new UsageFactorsReceivedBuilder()
            .WithFactors(new UsageFactor("24H", new DateOnly(2026, 9, 1), EstimatedUsageFactor: 4100.0m, ActualUsageFactor: null))
            .Build(), CancellationToken.None);

        _repository.Saves.Should().BeEmpty();
    }

    [Fact]
    public async Task RefusesAFactorThatBreaksTheContractAndSavesNothing()
    {
        var act = () => _handler.HandleAsync(new UsageFactorsReceivedBuilder()
            .WithFactors(new UsageFactor("24H", new DateOnly(2026, 10, 1), EstimatedUsageFactor: 4100.0m, ActualUsageFactor: 3842.5173m))
            .Build(), CancellationToken.None);

        await act.Should().ThrowAsync<ProfileStatementRejectedException>();
        _repository.Saves.Should().BeEmpty();
    }
}
