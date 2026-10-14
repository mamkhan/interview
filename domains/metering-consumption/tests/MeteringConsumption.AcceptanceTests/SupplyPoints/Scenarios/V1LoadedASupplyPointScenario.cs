using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Dapper;
using Microsoft.Data.SqlClient;
using Platform.Contracts;

namespace MeteringConsumption.AcceptanceTests.SupplyPoints.Scenarios;

// Publishes the builder's SupplyPointLoadedFromV1 for a new MPRN onto the platform's topic the way the V1 reader sends
// it, with no session and a message id naming the run and the MPRN, then reads the supply point's current rows and its
// usage factor history. The reader runs
// on-premises, outside every environment the suite reaches; its own end is proven by its integration tests.
public sealed class V1LoadedASupplyPointScenario : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private TargetEnvironment _environment = null!;

    public IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.MeterPointRow> MeterPoints { get; private set; } = [];

    public IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow> UsageFactors { get; private set; } = [];

    public IReadOnlyList<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow> UsageFactorHistory { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _environment = TargetEnvironment.Discover();

        // A new MPRN and a new run on every run keep the rows new, and the inbox never takes this load for an earlier one.
        var identity = Guid.NewGuid();
        var mprn = $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
        var startedAt = DateTimeOffset.UtcNow;
        var loaded = new SupplyPointLoadedFromV1Builder().ForMprn(mprn).WithEventId(identity).StartedAt(startedAt).Build();

        await using (var bus = _environment.ServiceBus())
        await using (var sender = bus.CreateSender(PlatformTopology.Topics.SupplyPointLoadedFromV1))
        {
            await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(loaded, Json))
            {
                MessageId = $"{loaded.CorrelationId}:{mprn}",
                ContentType = "application/json",
            }, ct);
        }

        // Reload writes the meter point and the usage factors in one transaction, so the first read that finds both holds
        // everything the load stated.
        (MeterPoints, UsageFactors) = await Polling.UntilAsync(
            async token => (await QueryAsync<EsbSentAnMm332ForANewSupplyPointScenario.MeterPointRow>(
                    EsbSentAnMm332ForANewSupplyPointScenario.MeterPointsSql("MeterPoint"), mprn, token),
                await QueryAsync<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow>(
                    EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorsSql("UsageFactor"), mprn, token)),
            rows => rows.Item1.Count > 0 && rows.Item2.Count > 0,
            rows => $"SupplyPoints holds {rows.Item1.Count} meter point and {rows.Item2.Count} usage factor rows for {mprn} loaded from V1.",
            ct);
        UsageFactorHistory = await QueryAsync<EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorRow>(
            EsbSentAnMm332ForANewSupplyPointScenario.UsageFactorsSql("UsageFactorHistory"), mprn, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<IReadOnlyList<TRow>> QueryAsync<TRow>(string sql, string mprn, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_environment.SqlConnectionString);
        return [.. await connection.QueryAsync<TRow>(new CommandDefinition(sql, new { Mprn = mprn }, cancellationToken: ct))];
    }
}
