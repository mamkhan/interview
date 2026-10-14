using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.IntegrationTests;

// A replacement entry, RRVN 1 of 0.123456 kWh a half-hour cancelled and RRVN 2 of 0.2 posted, written to the
// ledger container and read back: every value survives, including the six decimals, the read date, the
// millisecond posting time and the provenance.
public sealed class WhenAnEntryIsAppendedAndLoaded(WhenAnEntryIsAppendedAndLoaded.Appended appended)
    : IClassFixture<WhenAnEntryIsAppendedAndLoaded.Appended>
{
    [Fact]
    public void WritesOneFileUnderTheSupplyPointReadDateAndVersion() =>
        appended.Files.Should().Equal("10001234567/2026-06-20_2.parquet");

    [Fact]
    public void ReadsBackTheEntryAsItWasPosted() =>
        appended.Loaded.Single().Entry.Should().BeEquivalentTo(appended.Written.Entry);

    [Fact]
    public void ReadsBackBothLegsWithEveryDecimal()
    {
        var entry = appended.Loaded.Single().Entry;

        entry.Reversal!.Periods.Should().OnlyContain(period => period.Kwh == -0.123456m && period.Status == "VVAK");
        entry.Reading.Periods.Should().OnlyContain(period => period.Kwh == 0.2m && period.Status == "VCHG");
        entry.PostedAtUtc.Should().Be(new DateTimeOffset(2026, 6, 22, 2, 14, 0, 417, TimeSpan.Zero));
    }

    [Fact]
    public void ReadsBackTheFileTheReadingCameFrom() =>
        appended.Loaded.Single().Provenance.Should().Be(appended.Written.Provenance);

    public sealed class Appended : IAsyncLifetime
    {
        public RecordedEntry Written { get; private set; } = null!;

        public IReadOnlyList<RecordedEntry> Loaded { get; private set; } = [];

        public List<string> Files { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var ledger = await LedgerOnAzurite.CreateAsync(nameof(WhenAnEntryIsAppendedAndLoaded), ct);

            Written = new RecordedEntryBuilder()
                .Recording(new MeterDayReadingBuilder().WithUniformKwh(0.123456m).Build())
                .Recording(new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.2m).WithStatus("VCHG").Build())
                .Build()[1];

            await ledger.Store.AppendAsync(Written, ct);
            Loaded = await ledger.Store.LoadAsync(MeterDayReadingBuilder.MeterDay, ct);
            Files = await ledger.FilesAsync(ct);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
