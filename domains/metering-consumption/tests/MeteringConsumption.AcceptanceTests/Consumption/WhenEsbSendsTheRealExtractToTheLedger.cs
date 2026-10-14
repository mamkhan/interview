using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Features.Record.DTO;
using MeteringConsumption.Consumption.Domain;
using MeteringConsumption.Consumption.Infrastructure.DataLake;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.AcceptanceTests.Consumption;

// The repo's real MM343 extract is put in the running environment's drop container under a new name. Its two
// meter-days of 2026-06-24 land in the ledger with the day totals worked out from the file offline:
// IntervalValue × 0.5 summed over 48 half-hours, 8.7205 kWh for 10900000343 and 3.1865 for 10900001343. The
// same bytes on a later run are the same versions, so the entries stay the ones first kept; this run's own file is
// proven by its receipt.
public sealed class WhenEsbSendsTheRealExtractToTheLedger(WhenEsbSendsTheRealExtractToTheLedger.Sent sent)
    : IClassFixture<WhenEsbSendsTheRealExtractToTheLedger.Sent>
{
    private const string ExtractHash = "e00e47bdcc47d082e9117c6ee5ecaf7b383f73c719a96781eca8545f9e93b304";

    [Fact]
    public void ProcessesThisRunsFileIntoBothMeterDays()
    {
        sent.Receipt.Hash.Should().Be(ExtractHash);
        sent.Receipt.Outcome.Should().Be(MarketMessageOutcomes.Processed);
        sent.Receipt.ReadingsPublished.Should().Be(2);
    }

    [Fact]
    public void KeepsTheFirstSupplyPointsDayWithItsTotal()
    {
        var entry = sent.Entries["10900000343"].Entry;
        entry.Rrvn.Should().Be(1);
        entry.Reading.Periods[0].Kwh.Should().Be(0.070m);
        entry.Reading.TotalKwh.Should().Be(8.7205m);
    }

    [Fact]
    public void KeepsTheSecondSupplyPointsDayWithItsTotal()
    {
        var entry = sent.Entries["10900001343"].Entry;
        entry.Reading.Periods[0].Kwh.Should().Be(0.0345m);
        entry.Reading.TotalKwh.Should().Be(3.1865m);
    }

    [Fact]
    public void KeepsTheSealedFileTheReadingsCameFrom() =>
        sent.Entries.Values.Should().OnlyContain(recorded => recorded.Provenance.ContentHash == ExtractHash);

    public sealed class Sent : IAsyncLifetime
    {
        private const string SampleFile = "DSO_MIM_343_1782352213415_3805737.extract.xml";

        private static readonly string[] SupplyPoints = ["10900000343", "10900001343"];
        private static readonly DateOnly ReadDate = new(2026, 6, 24);

        public Receipts.ReceiptRow Receipt { get; private set; } = null!;

        public Dictionary<string, RecordedEntry> Entries { get; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var environment = TargetEnvironment.Discover();
            var drop = environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
            var ledger = new ParquetConsumptionLedgerStore(environment.Ledger(),
                new EntityNames(Options.Create(new EntityNamingOptions())));

            var fileName = $"DSO_MIM_343_{Guid.NewGuid():N}_extract_acceptance.xml";
            var extract = await File.ReadAllBytesAsync(Path.Combine("samples", SampleFile), ct);
            await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(extract), overwrite: false, ct);
            Receipt = await Receipts.WaitForTheOutcomeAsync(environment, fileName, ct);

            foreach (var mprn in SupplyPoints)
            {
                var meterDay = new MeterDay(mprn, ReadDate);
                var kept = await Polling.UntilAsync(
                    token => ledger.LoadAsync(meterDay, token),
                    entries => entries.Count > 0,
                    _ => $"the ledger holds nothing for {mprn} on {ReadDate}.",
                    ct);
                Entries[mprn] = kept[0];
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
