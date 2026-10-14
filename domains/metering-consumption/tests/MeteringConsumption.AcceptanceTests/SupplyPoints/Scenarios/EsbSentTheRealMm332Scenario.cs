using System.Text;
using Dapper;
using MeteringConsumption.MarketMessaging;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

// Drops the repo's real MM332 for MPRN 10900000332 into the drop container with the MPRN replaced by a new one, so this
// run's file and supply point are its own, then reads the supply point's usage factor history and current rows.
public sealed class EsbSentTheRealMm332Scenario : IAsyncLifetime
{
    private const string SampleFile = "ESB_MM332_10900000332_0000000790000332.xml";

    private TargetEnvironment _environment = null!;

    public IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow> UsageFactorHistory { get; private set; } = [];

    public IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow> UsageFactors { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _environment = TargetEnvironment.Discover();

        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var fileName = $"DSO_MIM_332_{identity:N}_real_acceptance.xml";
        var sample = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "samples", SampleFile), ct);
        var payload = Encoding.UTF8.GetBytes(sample.Replace("MPRN=\"10900000332\"", $"MPRN=\"{mprn}\"", StringComparison.Ordinal));

        var drop = _environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        await drop.GetBlobClient(fileName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false, ct);
        await Receipts.WaitForTheOutcomeAsync(_environment, fileName, ct);

        // Store writes both 24H statements in one transaction, so the first read that finds both history rows holds
        // everything the file stated.
        (UsageFactorHistory, UsageFactors) = await Polling.UntilAsync(
            async token => (await QueryAsync(EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorsSql("UsageFactorHistory"), mprn, token),
                await QueryAsync(EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorsSql("UsageFactor"), mprn, token)),
            rows => rows.Item1.Count >= 2,
            rows => $"SupplyPoints holds {rows.Item1.Count} usage factor history rows for {mprn} from {fileName}.",
            ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow>> QueryAsync(string sql, string mprn,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(_environment.SqlConnectionString);
        return [.. await connection.QueryAsync<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow>(
            new CommandDefinition(sql, new { Mprn = mprn }, cancellationToken: ct))];
    }
}
