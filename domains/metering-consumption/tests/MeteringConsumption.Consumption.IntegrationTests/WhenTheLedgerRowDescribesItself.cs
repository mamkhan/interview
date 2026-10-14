using AwesomeAssertions;
using Parquet;
using Parquet.Meta;

namespace MeteringConsumption.Consumption.IntegrationTests;

// A ledger file read by its own Parquet schema, as the data platform reads it: dates are DATE, timestamps are
// UTC milliseconds, kWh is DECIMAL(18,6), and every row names its leg, fuel, market and producer.
public sealed class WhenTheLedgerRowDescribesItself(WhenTheLedgerRowDescribesItself.Written written)
    : IClassFixture<WhenTheLedgerRowDescribesItself.Written>
{
    [Fact]
    public void HasTheLedgerColumns() =>
        written.Columns.Keys.Should().BeEquivalentTo(
            "LedgerEntryId", "SupplyPointId", "MeterSerial", "ReadDate", "EntryType", "Rrvn", "SupersedesRrvn",
            "Leg", "PostedAtUtc", "Period", "Kwh", "Status", "MeteringIntervalMinutes", "UomCode",
            "SourceMessageType", "MarketTimestamp", "BlobPath", "ContentHash", "ReceivedFileName",
            "Fuel", "Market", "Producer");

    [Fact]
    public void TypesTheReadDateAsADate() => written.Columns["ReadDate"].LogicalType!.DATE.Should().NotBeNull();

    [Fact]
    public void TypesTimestampsAsUtcMilliseconds()
    {
        foreach (var column in new[] { "PostedAtUtc", "MarketTimestamp" })
        {
            var timestamp = written.Columns[column].LogicalType!.TIMESTAMP!;
            timestamp.IsAdjustedToUTC.Should().BeTrue(column);
            timestamp.Unit.MILLIS.Should().NotBeNull(column);
        }
    }

    [Fact]
    public void TypesKwhAsDecimalEighteenSix()
    {
        written.Columns["Kwh"].LogicalType!.DECIMAL!.Precision.Should().Be(18);
        written.Columns["Kwh"].LogicalType!.DECIMAL!.Scale.Should().Be(6);
    }

    [Fact]
    public void TypesVersionsAndPeriodsAsThirtyTwoBitIntegers()
    {
        foreach (var column in new[] { "Rrvn", "SupersedesRrvn", "Period", "MeteringIntervalMinutes" })
        {
            written.Columns[column].Type.Should().Be(Parquet.Meta.Type.INT32, column);
        }
    }

    public sealed class Written : IAsyncLifetime
    {
        public Dictionary<string, SchemaElement> Columns { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var ledger = await LedgerOnAzurite.CreateAsync(nameof(WhenTheLedgerRowDescribesItself), ct);
            var entries = new RecordedEntryBuilder()
                .Recording(new MeterDayReadingBuilder().Build())
                .Recording(new MeterDayReadingBuilder().WithRrvn(2).WithUniformKwh(0.2m).Build())
                .Build();
            await ledger.Store.AppendAsync(entries[1], ct);

            var file = (await ledger.FilesAsync(ct)).Single();
            using var content = new MemoryStream((await ledger.Container.GetBlobClient(file).DownloadContentAsync(ct)).Value.Content.ToArray());
            using var reader = await ParquetReader.CreateAsync(content, cancellationToken: ct);
            Columns = reader.Metadata!.Schema.Skip(1).ToDictionary(element => element.Name);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
