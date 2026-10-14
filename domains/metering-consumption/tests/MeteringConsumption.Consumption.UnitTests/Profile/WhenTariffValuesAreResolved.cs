using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeteringConsumption.Consumption.UnitTests.Profile;

// The V1 reader's TariffResolved for a supply point that has no profile yet: a 48-rate tariff on the 24-hour curve
// with load profile LP1, read at 2026-10-07T04:00Z.
public class WhenTariffValuesAreResolved
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);

    private readonly FakeProfileRepository _repository = new();

    public WhenTariffValuesAreResolved()
    {
        new ProfileTariffResolvedEventHandler(_repository, new FixedTimeProvider(Now),
                NullLogger<ProfileTariffResolvedEventHandler>.Instance)
            .HandleAsync(new TariffResolvedBuilder().Build(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void StoresTheTariffValues()
    {
        var tariff = _repository.Stored("10012345678")!.Tariff!;
        tariff.IsTou.Should().BeTrue();
        tariff.CalcType.Should().Be("TwentyFourHour");
        tariff.EffectiveLoadProfileCode.Should().Be("LP1");
    }

    [Fact]
    public void RecordsWhenTheValuesWereReadAndWhoResolvedThem()
    {
        var tariff = _repository.Stored("10012345678")!.Tariff!;
        tariff.AsOf.Should().Be(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        tariff.Source.Should().Be("v1-reader/tariff-values");
    }

    [Fact]
    public void KeysANewProfileOnTheMprnAndTheMarket() =>
        _repository.Stored("10012345678")!.Market.Should().Be("roi-esb");

    [Fact]
    public void SavesTheTariffAndNothingElse() => _repository.Saves.Should().Equal("tariff");
}

// The same tariff values arrive on the next night's run.
public class WhenTheSameTariffValuesAreResolvedAgain
{
    private readonly FakeProfileRepository _repository = new();

    public WhenTheSameTariffValuesAreResolvedAgain()
    {
        var stored = SupplyPointProfile.New("10012345678", "roi-esb");
        stored.Apply(new TariffStatementBuilder().Build());
        _repository.Seed(stored);

        new ProfileTariffResolvedEventHandler(_repository,
                new FixedTimeProvider(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero)),
                NullLogger<ProfileTariffResolvedEventHandler>.Instance)
            .HandleAsync(new TariffResolvedBuilder().ReadAt(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero)).Build(),
                CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void SavesNothing() => _repository.Saves.Should().BeEmpty();
}

// The reader always stamps AsOf, which orders one night's values against another's; an event without it cannot be
// ordered, so it is refused.
public class WhenTariffValuesArriveWithoutAsOf
{
    [Fact]
    public async Task RefusesTheEvent()
    {
        var handler = new ProfileTariffResolvedEventHandler(new FakeProfileRepository(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero)),
            NullLogger<ProfileTariffResolvedEventHandler>.Instance);

        var act = () => handler.HandleAsync(new TariffResolvedBuilder().WithoutAsOf().Build(), CancellationToken.None);

        await act.Should().ThrowAsync<ProfileStatementRejectedException>().WithMessage("*AsOf*");
    }
}
