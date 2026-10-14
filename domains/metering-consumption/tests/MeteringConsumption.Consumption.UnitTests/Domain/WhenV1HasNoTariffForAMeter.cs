using AwesomeAssertions;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.UnitTests.Domain;

// The profile holds a 48-rate tariff read at 2026-10-07T04:00Z, and the next night V1 holds no tariff for the
// meter. The profile must then show none, as V1 does, so the empty values replace the stored ones.
public class WhenV1HasNoTariffForAMeter
{
    private readonly SupplyPointProfile _profile = SupplyPointProfile.New("10012345678", "roi-esb");
    private readonly bool _changed;

    public WhenV1HasNoTariffForAMeter()
    {
        _profile.Apply(new TariffStatementBuilder().Build());
        _changed = _profile.Apply(new TariffStatementBuilder()
            .WithNoTariff()
            .ReadAt(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero))
            .Build());
    }

    [Fact]
    public void ReportsAChange() => _changed.Should().BeTrue();

    [Fact]
    public void ClearsEveryTariffValue()
    {
        _profile.Tariff!.IsTou.Should().BeNull();
        _profile.Tariff.CalcType.Should().BeNull();
        _profile.Tariff.EffectiveLoadProfileCode.Should().BeNull();
    }

    [Fact]
    public void RecordsWhenTheTariffWasFoundMissing() =>
        _profile.Tariff!.AsOf.Should().Be(new DateTimeOffset(2026, 10, 8, 4, 0, 0, TimeSpan.Zero));
}
