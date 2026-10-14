using AwesomeAssertions;
using BuildingBlocks.TestsShared;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.Persistence.Repositories;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.IntegrationTests;

// The Profile writes a supply point's tariff values and smart meter flag through its own repository; Forecast's
// reader reads them back, each group with when it was read and who stated it.
public sealed class WhenReadingTheProfilesTariffValues : IAsyncLifetime
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 10, 12, 21, 0, 0, 123, TimeSpan.Zero);

    private SupplyPointProfileRepository _profiles = null!;
    private SupplyPointTariffReadRepository _reader = null!;

    public async ValueTask InitializeAsync()
    {
        var scope = await SuiteScope.CreateAsync(Suite.Id, nameof(WhenReadingTheProfilesTariffValues),
            new SuiteResources { Migrate = ConsumptionSchema.ApplyConsumptionSchemaAsync },
            TestContext.Current.CancellationToken);
        _profiles = new SupplyPointProfileRepository(scope.SqlConnectionString!);
        _reader = new SupplyPointTariffReadRepository(scope.SqlConnectionString!);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReadsTheTariffValuesAndFlagTheProfileStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var tariff = new TariffStatementBuilder().Build();
        var flag = new SmartMeterFlagStatementBuilder().Build();
        var profile = SupplyPointProfile.New("10000000011", "roi-esb");
        profile.Apply(tariff);
        profile.Apply(flag);
        await _profiles.SaveTariffAsync(profile, UpdatedAt, ct);
        await _profiles.SaveSmartMeterFlagAsync(profile, UpdatedAt, ct);

        var read = await _reader.FindAsync("10000000011", ct);

        read!.Facts.Should().Be(new TariffFacts(flag.IsNsmp, tariff.IsTou, tariff.CalcType, tariff.EffectiveLoadProfileCode));
        read.TariffAsOf.Should().Be(tariff.AsOf);
        read.TariffSource.Should().Be(tariff.Source);
        read.SmartMeterFlagAsOf.Should().Be(flag.AsOf);
        read.SmartMeterFlagSource.Should().Be(flag.Source);
    }

    [Fact]
    public async Task ReadsEmptyValuesForAGroupTheProfileHasNotReceived()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = SupplyPointProfile.New("10000000012", "roi-esb");
        profile.Apply(new SmartMeterFlagStatementBuilder().Build());
        await _profiles.SaveSmartMeterFlagAsync(profile, UpdatedAt, ct);

        var read = await _reader.FindAsync("10000000012", ct);

        read!.Facts.IsTou.Should().BeNull();
        read.Facts.CalcType.Should().BeNull();
        read.TariffAsOf.Should().BeNull();
    }

    [Fact]
    public async Task ReadsNothingForASupplyPointWithNoProfile() =>
        (await _reader.FindAsync("10000000013", TestContext.Current.CancellationToken)).Should().BeNull();
}
