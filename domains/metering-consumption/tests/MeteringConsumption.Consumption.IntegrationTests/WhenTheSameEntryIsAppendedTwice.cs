using AwesomeAssertions;

namespace MeteringConsumption.Consumption.IntegrationTests;

// A redelivery appends the entry it already appended: the path is its key, so the second write overwrites the
// first with the same bytes and the day still has one version.
public sealed class WhenTheSameEntryIsAppendedTwice(WhenTheSameEntryIsAppendedTwice.Appended appended)
    : IClassFixture<WhenTheSameEntryIsAppendedTwice.Appended>
{
    [Fact]
    public void KeepsOneFile() => appended.Files.Should().ContainSingle();

    [Fact]
    public void WritesTheSameBytes() => appended.Second.Should().Equal(appended.First);

    [Fact]
    public void LoadsOneVersion() => appended.LoadedCount.Should().Be(1);

    public sealed class Appended : IAsyncLifetime
    {
        public byte[] First { get; private set; } = [];

        public byte[] Second { get; private set; } = [];

        public List<string> Files { get; private set; } = [];

        public int LoadedCount { get; private set; }

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;
            var ledger = await LedgerOnAzurite.CreateAsync(nameof(WhenTheSameEntryIsAppendedTwice), ct);
            var entry = new RecordedEntryBuilder().Recording(new MeterDayReadingBuilder().Build()).Build()[0];

            await ledger.Store.AppendAsync(entry, ct);
            Files = await ledger.FilesAsync(ct);
            First = (await ledger.Container.GetBlobClient(Files[0]).DownloadContentAsync(ct)).Value.Content.ToArray();

            await ledger.Store.AppendAsync(entry, ct);
            Second = (await ledger.Container.GetBlobClient(Files[0]).DownloadContentAsync(ct)).Value.Content.ToArray();
            Files = await ledger.FilesAsync(ct);
            LoadedCount = (await ledger.Store.LoadAsync(MeterDayReadingBuilder.MeterDay, ct)).Count;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
