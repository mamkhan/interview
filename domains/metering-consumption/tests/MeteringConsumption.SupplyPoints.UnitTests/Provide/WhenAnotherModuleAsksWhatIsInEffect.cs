using AwesomeAssertions;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.SupplyPoints.Application.Features.Provide;

namespace MeteringConsumption.SupplyPoints.UnitTests.Provide;

// 10000000001 holds the Diagrams 4 example, recorded on 2026-10-09: meter point A from 2026-03-01 (MCC01, 01, DG1, E),
// B from 2026-06-15 (MCC02, 02), C from 2026-11-01 (status D) and a backdated MM301 from 2026-05-01 (DG2); the 24H
// estimate 4127.5 from 2026-03-01; the 00D estimate 1500.0 from 2026-03-01 and actual 1400.0 from 2026-06-15. Current
// holds the 2026-06-15 meter point row (MCC02, 02, DG2, E: B took DG2 as it stated no DUoS group), the 2026-11-01 row
// (MCC02, 02, DG2, D), the 24H row from 2026-03-01 and the 00D row from 2026-06-15 (1500.0, 1400.0).
public class WhenAnotherModuleAsksWhatIsInEffect
{
    private static readonly DateOnly RecordedOn = new(2026, 10, 9);

    private readonly FakeSupplyPointReader _reader = new();
    private readonly MeterPointConfigurationQuery _configuration;

    public WhenAnotherModuleAsksWhatIsInEffect()
    {
        var meterPoint = _reader.MeterPoint("10000000001");
        meterPoint.Record(new MeterPointStatementBuilder().Build(), RecordedOn);
        meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 6, 15)).WithCodes("MCC02", "02", null, null).WithNetworksReferenceNumber("NRN-hash-mm332").Build(), RecordedOn);
        meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 11, 1)).WithCodes(null, null, null, "D").WithNetworksReferenceNumber("NRN-hash-mm306").Build(), RecordedOn);
        meterPoint.Record(new MeterPointStatementBuilder()
            .StartingOn(new DateOnly(2026, 5, 1)).WithCodes(null, null, "DG2", null).WithNetworksReferenceNumber("NRN-hash-mm301").Build(), RecordedOn);
        _reader.UsageFactor("10000000001", "24H").Record(new SupplyPointUsageFactorStatementBuilder().Build(), RecordedOn);
        _reader.UsageFactor("10000000001", "00D").Record(new SupplyPointUsageFactorStatementBuilder().WithValues(1500.0m, null).Build(), RecordedOn);
        _reader.UsageFactor("10000000001", "00D").Record(new SupplyPointUsageFactorStatementBuilder()
            .StartingOn(new DateOnly(2026, 6, 15)).WithValues(null, 1400.0m).Build(), RecordedOn);
        _configuration = new MeterPointConfigurationQuery(_reader);
    }

    [Fact]
    public async Task AnswersTodayWithTodaysRows() =>
        (await _configuration.InEffectOnAsync("10000000001", RecordedOn, CancellationToken.None)).Should().BeEquivalentTo(
            new MeterPointConfiguration
            {
                MPRN = "10000000001",
                Day = RecordedOn,
                MeterConfigurationCode = "MCC02",
                LoadProfileCode = "02",
                DuosGroup = "DG2",
                MeterPointStatusCode = "E",
                UsageFactors = [new UsageFactorInEffect("00D", 1500.0m, 1400.0m), new UsageFactorInEffect("24H", 4127.5m, null)],
            });

    [Fact]
    public async Task AnswersAFutureDayWithTheRowAlreadyKnownForIt() =>
        (await _configuration.InEffectOnAsync("10000000001", new DateOnly(2026, 11, 15), CancellationToken.None))!
            .MeterPointStatusCode.Should().Be("D");

    // No current row is that old, so each value is the latest stated on or before the day: DG2 from the MM301, and the
    // configuration, load profile, status and 00D estimate that only the 2026-03-01 statements gave.
    [Fact]
    public async Task AnswersADayBeforeCurrentWithWhatWasStatedByThen() =>
        (await _configuration.InEffectOnAsync("10000000001", new DateOnly(2026, 5, 20), CancellationToken.None)).Should().BeEquivalentTo(
            new MeterPointConfiguration
            {
                MPRN = "10000000001",
                Day = new DateOnly(2026, 5, 20),
                MeterConfigurationCode = "MCC01",
                LoadProfileCode = "01",
                DuosGroup = "DG2",
                MeterPointStatusCode = "E",
                UsageFactors = [new UsageFactorInEffect("00D", 1500.0m, null), new UsageFactorInEffect("24H", 4127.5m, null)],
            });

    [Fact]
    public async Task AnswersNothingForASupplyPointItHasNotSeen() =>
        (await _configuration.InEffectOnAsync("10000000099", new DateOnly(2026, 5, 20), CancellationToken.None)).Should().BeNull();

    [Fact]
    public async Task AnswersNothingForADayBeforeAnythingWasInEffect() =>
        (await _configuration.InEffectOnAsync("10000000001", new DateOnly(2026, 2, 28), CancellationToken.None)).Should().BeNull();
}
