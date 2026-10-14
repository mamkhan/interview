using AwesomeAssertions;
using MeteringConsumption.Consumption.Application.Features.Forecast.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.TestsShared;

namespace MeteringConsumption.Consumption.IntegrationTests;

// Entries written by Record's own store, read as of 2026-06-05 with a 29-day window. 10001234567 read from 2026-06-01 to
// 06-05 at 0.25 kWh a half-hour, with 06-03 revised to RRVN 2 at 0.5 kWh and an older day on 2026-05-01; its window
// starts on 05-08. 10001234568 last read on 2026-03-05, long before the months the window from 06-05 covers.
// 10001234569 last read on 2026-05-10, so its window starts on 04-12, a month earlier than the window from 06-05.
public sealed class WhenReadingRecentLedgerDays(WhenReadingRecentLedgerDays.Written written)
    : IClassFixture<WhenReadingRecentLedgerDays.Written>
{
    private const string SupplyPointId = MeterDayReadingBuilder.SupplyPointId;

    [Fact]
    public void ReadsEachRecentDayOnceOldestFirst() =>
        written.Days.Select(day => day.Day.ReadDate).Should().Equal(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 3),
            new DateOnly(2026, 6, 4), new DateOnly(2026, 6, 5));

    [Fact]
    public void TakesTheHighestVersionOfARevisedDay()
    {
        var revised = written.Days.Single(day => day.Day.ReadDate == new DateOnly(2026, 6, 3));

        revised.Rrvn.Should().Be(2);
        revised.LedgerEntryId.Should().EndWith($":{SupplyPointId}:2026-06-03:2");
        revised.Day.TotalKwh.Should().Be(24m);
    }

    [Fact]
    public void ReadsTheDayInFullFromItsReadingLeg() =>
        written.Days[0].Day.Slots.Should().HaveCount(48).And.OnlyContain(kwh => kwh == 0.25m);

    [Fact]
    public void FindsAMetersLastReadsLongBeforeTheDayAskedAbout() =>
        written.Stopped.Select(day => day.Day.ReadDate).Should().Equal(
            new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5));

    [Fact]
    public void ReachesBackIntoTheMonthTheWindowStartsIn() =>
        written.Lapsed.Select(day => day.Day.ReadDate).Should().Equal(
            new DateOnly(2026, 4, 20), new DateOnly(2026, 5, 10));

    [Fact]
    public void ReadsNothingForASupplyPointTheLedgerDoesNotHold() =>
        written.Unknown.Should().BeEmpty();

    public sealed class Written : IAsyncLifetime
    {
        private static readonly DateOnly AsOf = new(2026, 6, 5);

        public IReadOnlyList<LedgerDay> Days { get; private set; } = [];

        public IReadOnlyList<LedgerDay> Stopped { get; private set; } = [];

        public IReadOnlyList<LedgerDay> Lapsed { get; private set; } = [];

        public IReadOnlyList<LedgerDay> Unknown { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var ledger = await LedgerOnAzurite.CreateAsync(nameof(WhenReadingRecentLedgerDays), ct);

            await AppendAsync(ledger, new MeterDay(SupplyPointId, new DateOnly(2026, 5, 1)), ct);
            for (var day = 1; day <= 5; day++)
            {
                await AppendAsync(ledger, new MeterDay(SupplyPointId, new DateOnly(2026, 6, day)), ct);
            }

            var revised = new MeterDay(SupplyPointId, new DateOnly(2026, 6, 3));
            var versions = new RecordedEntryBuilder(revised)
                .Recording(new MeterDayReadingBuilder().ForDay(revised).Build())
                .Recording(new MeterDayReadingBuilder().ForDay(revised).WithRrvn(2).WithUniformKwh(0.5m).Build())
                .Build();
            await ledger.Store.AppendAsync(versions[1], ct);

            await AppendAsync(ledger, new MeterDay("10001234568", new DateOnly(2026, 3, 4)), ct);
            await AppendAsync(ledger, new MeterDay("10001234568", new DateOnly(2026, 3, 5)), ct);
            await AppendAsync(ledger, new MeterDay("10001234569", new DateOnly(2026, 4, 20)), ct);
            await AppendAsync(ledger, new MeterDay("10001234569", new DateOnly(2026, 5, 10)), ct);

            Days = await ledger.Reader.ReadRecentDaysAsync(SupplyPointId, AsOf, 29, ct);
            Stopped = await ledger.Reader.ReadRecentDaysAsync("10001234568", AsOf, 29, ct);
            Lapsed = await ledger.Reader.ReadRecentDaysAsync("10001234569", AsOf, 29, ct);
            Unknown = await ledger.Reader.ReadRecentDaysAsync("10000000000", AsOf, 29, ct);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Task AppendAsync(LedgerOnAzurite ledger, MeterDay meterDay, CancellationToken ct) =>
            ledger.Store.AppendAsync(
                new RecordedEntryBuilder(meterDay).Recording(new MeterDayReadingBuilder().ForDay(meterDay).Build()).Build()[0], ct);
    }
}
