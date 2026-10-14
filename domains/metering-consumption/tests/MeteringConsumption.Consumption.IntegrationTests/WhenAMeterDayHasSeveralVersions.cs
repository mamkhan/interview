using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.IntegrationTests;

// Three versions of one meter-day appended out of order, 0.25, 0.20 and 0.22 kWh a half-hour, beside a version
// of the next day that must not be read with them. Every file sits in the supply point's folder, named for its
// day and version. Loading the day returns its three entries by RRVN, and the ledger rebuilt from them has RRVN 3
// in effect.
public sealed class WhenAMeterDayHasSeveralVersions(WhenAMeterDayHasSeveralVersions.Appended appended)
    : IClassFixture<WhenAMeterDayHasSeveralVersions.Appended>
{
    [Fact]
    public void KeepsEachDayAndVersionAsAFileInTheSupplyPointsFolder() =>
        appended.Files.Should().Equal(
            "10001234567/2026-06-20_1.parquet",
            "10001234567/2026-06-20_2.parquet",
            "10001234567/2026-06-20_3.parquet",
            "10001234567/2026-06-21_1.parquet");

    [Fact]
    public void LoadsEveryVersionOfTheDayOrderedByRrvn() =>
        appended.Loaded.Select(recorded => recorded.Entry.Rrvn).Should().Equal(1, 2, 3);

    [Fact]
    public void RebuildsTheLedgerWithTheLatestVersionInEffect()
    {
        var ledger = MeterDayLedger.Rehydrate(MeterDayReadingBuilder.MeterDay, appended.Loaded.Select(recorded => recorded.Entry));

        var next = ledger.Record(new MeterDayReadingBuilder().WithRrvn(4).Build(), DateTimeOffset.MinValue);

        var reversal = next.Should().BeOfType<RecordOutcome.Recorded>().Subject.Entry.Reversal!;
        reversal.Rrvn.Should().Be(3);
        reversal.Periods.Should().OnlyContain(period => period.Kwh == -0.22m);
    }

    public sealed class Appended : IAsyncLifetime
    {
        public IReadOnlyList<RecordedEntry> Loaded { get; private set; } = [];

        public List<string> Files { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var ledger = await LedgerOnAzurite.CreateAsync(nameof(WhenAMeterDayHasSeveralVersions), ct);

            var entries = new RecordedEntryBuilder()
                .Recording(new MeterDayReadingBuilder().WithUniformKwh(0.25m).Build())
                .Recording(new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.20m).Build())
                .Recording(new MeterDayReadingBuilder().WithRrvn(3).WithUniformKwh(0.22m).Build())
                .Build();
            var nextDay = entries[0] with
            {
                Entry = entries[0].Entry with { MeterDay = new MeterDay(MeterDayReadingBuilder.SupplyPointId, new DateOnly(2026, 6, 21)) },
            };

            foreach (var recorded in new[] { entries[2], nextDay, entries[0], entries[1] })
            {
                await ledger.Store.AppendAsync(recorded, ct);
            }

            Loaded = await ledger.Store.LoadAsync(MeterDayReadingBuilder.MeterDay, ct);
            Files = await ledger.FilesAsync(ct);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
