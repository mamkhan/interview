using AwesomeAssertions;
using MeteringConsumption.Contracts.Events.Consumption;

namespace MeteringConsumption.Consumption.UnitTests.Record;

// RRVN 1 at 0.5 KWT a half-hour (0.25 kWh) is kept when RRVN 2 at 0.4 KWT (0.20 kWh, VCHG) arrives in another
// file: the announcement carries RRVN 1 cancelled, -0.25 × 48 = -12.00, beside RRVN 2 in full, 0.20 × 48 = +9.60.
public sealed class WhenHandlingAReplacement : IAsyncLifetime
{
    private readonly RecordHandling _handling = new();

    private ConsumptionRecorded Replacement => _handling.Announced.Last();

    public async ValueTask InitializeAsync()
    {
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().Build());
        await _handling.DeliverAsync(new IntervalReadingReceivedBuilder().FromFile("revision").WithRrvn(2)
            .WithUniformValue(0.40m, "VCHG").Build());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void AnnouncesAReplacementOfRrvnOne()
    {
        Replacement.EntryType.Should().Be("ReplacementReadingReceived");
        Replacement.RRVN.Should().Be(2);
        Replacement.SupersedesRRVN.Should().Be(1);
        Replacement.LedgerEntryId.Should().Be("revision:10001234567:2026-06-20:2");
    }

    [Fact]
    public void CarriesRrvnOneCancelledBesideRrvnTwoInFull()
    {
        Replacement.Reversal!.RRVN.Should().Be(1);
        Replacement.Reversal.Periods.Should().OnlyContain(period => period.Kwh == -0.25m && period.Status == "VVAK");
        Replacement.Reversal.TotalKwh.Should().Be(-12.00m);
        Replacement.Reading.Periods.Should().OnlyContain(period => period.Kwh == 0.20m && period.Status == "VCHG");
        Replacement.Reading.TotalKwh.Should().Be(9.60m);
    }

    [Fact]
    public void KeepsBothVersions() => _handling.Store.Kept.Select(kept => kept.Entry.Rrvn).Should().Equal(1, 2);

    [Fact]
    public void CarriesTheRevisionsFile() => Replacement.Provenance.ContentHash.Should().Be("revision");
}
