using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.MarketMessaging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.AcceptanceTests.Consumption;

// Two MM343 files for one new supply point's 2026-06-24 are put in the running environment's drop container:
// RRVN 1 at IntervalValue 0.5 (0.25 kWh a half-hour, 12.00 for the day), then RRVN 2 at 0.4 (0.20, 9.60).
// The ledger then holds both versions, the second cancelling the first in full. A new MPRN on every run keeps
// the meter-day new.
public sealed class WhenEsbRevisesAMeterDay(WhenEsbRevisesAMeterDay.Revised revised)
    : IClassFixture<WhenEsbRevisesAMeterDay.Revised>
{
    [Fact]
    public void KeepsTheFirstVersionInFull()
    {
        var first = revised.Entries[0].Entry;
        first.EntryType.Should().Be(EntryType.ReadingReceived);
        first.Reading.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == 0.25m);
        first.Reading.TotalKwh.Should().Be(12.00m);
    }

    [Fact]
    public void CancelsTheFirstVersionWhenTheRevisionArrives()
    {
        var revision = revised.Entries[1].Entry;
        revision.SupersedesRrvn.Should().Be(1);
        revision.Reversal!.TotalKwh.Should().Be(-12.00m);
        revision.Reading.Periods.Should().HaveCount(48).And.OnlyContain(period => period.Kwh == 0.20m);
        revision.Reading.TotalKwh.Should().Be(9.60m);
    }

    [Fact]
    public void KeepsEachVersionWithTheFileItCameFrom() =>
        revised.Entries.Select(recorded => recorded.Provenance.ReceivedFileName).Should().Equal(revised.FileNames);

    public sealed class Revised : IAsyncLifetime
    {
        private static readonly DateOnly ReadDate = new(2026, 6, 24);

        public IReadOnlyList<RecordedEntry> Entries { get; private set; } = [];

        public List<string> FileNames { get; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var environment = TargetEnvironment.Discover();
            var drop = environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
            var ledger = new ParquetConsumptionLedgerStore(environment.Ledger(),
                new EntityNames(Options.Create(new EntityNamingOptions())));

            var identity = Guid.NewGuid();
            var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
            var meterDay = new MeterDay(mprn, ReadDate);

            foreach (var (rrvn, intervalValue) in new[] { (1, 0.5m), (2, 0.4m) })
            {
                var fileName = $"DSO_MIM_343_{identity:N}_{rrvn}_acceptance.xml";
                FileNames.Add(fileName);
                var payload = new MarketMessageXmlBuilder().WithMprn(mprn).WithReadDate(ReadDate).WithRrvn(rrvn)
                    .WithIntervalValue(intervalValue).BuildBytes();
                await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);
                Entries = await Polling.UntilAsync(
                    token => ledger.LoadAsync(meterDay, token),
                    entries => entries.Count == rrvn,
                    entries => $"the ledger holds {entries.Count} version(s) of {mprn} on {ReadDate}, not {rrvn}.",
                    ct);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
