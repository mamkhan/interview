using System.Data;
using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Dapper;
using Growth.Contracts;
using MeteringConsumption.Contracts;
using MeteringConsumption.MarketMessaging;
using MeteringConsumption.TestsShared;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.AcceptanceTests.Consumption.Scenarios;

// A new MPRN set up the way production sets one up, then read for eight days. SupplyPoints learns its configuration
// from a dropped MM332 in effect from 2026-06-12 (MCC01, profile 01, DG1); the Profile learns its tariff and smart
// meter flag from the V1 reader's events (time-of-use, 24-hour, LP1, NSMP). Reference data holds the real LP1 curve
// and DG1's 2026 averages. Then 12 kWh a day from 2026-06-13 to 06-20 is published as Market Messaging would.
public sealed class ReadingsRecordedForAForecastMeterScenario : IAsyncLifetime
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BetweenPolls = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly DateOnly FirstReadDate = new(2026, 6, 13);
    public const int ReadDays = 8;

    private const string MeterPointSql = """
        SELECT COUNT(*) FROM supply.MeterPoint AS m JOIN supply.SupplyPoint AS s ON s.SupplyPointId = m.SupplyPointId
        WHERE s.XPRN = @Mprn;
        """;

    private const string ProfileSql = """
        SELECT COUNT(*) FROM dbo.SupplyPointProfile WHERE SupplyPointId = @Mprn AND IsTou IS NOT NULL AND IsNsmp IS NOT NULL;
        """;

    private const string ForecastSql = """
        SELECT ForecastVersion, AnchorDate, HorizonDays, TotalKwh, UsageFactorSource, Daily, Inputs
        FROM dbo.ConsumptionForecast
        WHERE SupplyPointId = @Mprn AND IsCurrent = 1 AND TriggerLedgerEntryId = @Trigger;
        """;

    private TargetEnvironment _environment = null!;

    public string Mprn { get; private set; } = "";

    public ForecastRow Forecast { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _environment = TargetEnvironment.Discover();
        Mprn = ProfileEvents.NewMprn();

        await SeedReferenceDataAsync(ct);

        var drop = _environment.Blobs().GetBlobContainerClient(MarketMessagingTopology.Containers.EsbDrop);
        var mm332 = new MarketMessageXmlBuilder().WithMessageTypeCode("332").WithMprn(Mprn).WithReadDate(new DateOnly(2026, 6, 12));
        await drop.GetBlobClient($"DSO_MIM_332_{Mprn}_forecast_acceptance.xml").UploadAsync(BinaryData.FromBytes(mm332.BuildBytes()), overwrite: false, ct);
        await WaitForAsync(MeterPointSql, new { Mprn }, "SupplyPoints' meter point", ct);

        await using var bus = _environment.ServiceBus();
        await SendAsync(bus, GrowthTopology.Topics.TariffResolved, new TariffResolvedBuilder().ForMprn(Mprn).Build(),
            $"v1-reader:{Mprn}:TariffResolved", ct);
        await SendAsync(bus, MeteringConsumptionTopology.Topics.MeterConfigurationReceived,
            new MeterConfigurationReceivedBuilder().ForMprn(Mprn).AsTheV1ReaderStatesIt(isNsmp: true).Build(),
            $"v1-reader:{Mprn}:MeterConfigurationReceived", ct);
        await WaitForAsync(ProfileSql, new { Mprn }, "the Profile's tariff values and smart meter flag", ct);

        var trigger = "";
        for (var day = 0; day < ReadDays; day++)
        {
            var reading = new IntervalReadingReceivedBuilder().ForMprn(Mprn).OnDate(FirstReadDate.AddDays(day)).Build();
            await SendAsync(bus, MeteringConsumptionTopology.Topics.IntervalReadingReceived, reading, reading.ReadingId, ct);
            trigger = reading.ReadingId;
        }

        var giveUpAfter = DateTimeOffset.UtcNow + Deadline;
        while (DateTimeOffset.UtcNow < giveUpAfter)
        {
            await using var connection = new SqlConnection(_environment.SqlConnectionString);
            if (await connection.QuerySingleOrDefaultAsync<ForecastRow>(
                    new CommandDefinition(ForecastSql, new { Mprn, Trigger = trigger }, cancellationToken: ct)) is { } row)
            {
                Forecast = row;
                return;
            }

            await Task.Delay(BetweenPolls, ct);
        }

        throw new InvalidOperationException($"No current forecast for {Mprn} from {trigger} appeared within {Deadline}.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task SendAsync<TEvent>(ServiceBusClient bus, string topic, TEvent @event, string messageId, CancellationToken ct)
    {
        await using var sender = bus.CreateSender(topic);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = messageId,
            SessionId = Mprn,
            ContentType = "application/json",
        }, ct);
    }

    private async Task WaitForAsync(string sql, object parameters, string what, CancellationToken ct)
    {
        var giveUpAfter = DateTimeOffset.UtcNow + Deadline;
        while (DateTimeOffset.UtcNow < giveUpAfter)
        {
            await using var connection = new SqlConnection(_environment.SqlConnectionString);
            if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: ct)) > 0)
            {
                return;
            }

            await Task.Delay(BetweenPolls, ct);
        }

        throw new InvalidOperationException($"{what} for {Mprn} did not appear within {Deadline}.");
    }

    // Writes only what is missing, so the rows another process loaded, or an earlier run, stay as they are.
    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        var table = new DataTable();
        table.Columns.Add("LoadProfileCode", typeof(string));
        table.Columns.Add("ValueDate", typeof(DateTime));
        table.Columns.Add("Hour", typeof(byte));
        table.Columns.Add("H24", typeof(decimal));
        foreach (var fields in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "samples", "v1-load-profile-hours.csv"))
                     .Skip(1).Select(line => line.Split(',')).Where(fields => fields[0] == "LP1"))
        {
            table.Rows.Add(fields[0], DateTime.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                byte.Parse(fields[2], CultureInfo.InvariantCulture), decimal.Parse(fields[3], CultureInfo.InvariantCulture));
        }

        await using var connection = new SqlConnection(_environment.ReferenceDataConnectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE #Lp1 (LoadProfileCode varchar(8), ValueDate date, [Hour] tinyint, H24 decimal(12,10));
            """, cancellationToken: ct));
        using (var bulk = new SqlBulkCopy(connection) { DestinationTableName = "#Lp1" })
        {
            await bulk.WriteToServerAsync(table, ct);
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO dbo.LoadProfileHourlyValue (LoadProfileCode, ValueDate, [Hour], H24)
            SELECT s.LoadProfileCode, s.ValueDate, s.[Hour], s.H24 FROM #Lp1 AS s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.LoadProfileHourlyValue AS v
                              WHERE v.LoadProfileCode = s.LoadProfileCode AND v.ValueDate = s.ValueDate AND v.[Hour] = s.[Hour]);

            IF NOT EXISTS (SELECT 1 FROM dbo.AnnualAverageUsage WHERE DuosGroup = 'DG1' AND [Year] = 2026)
                INSERT INTO dbo.AnnualAverageUsage (DuosGroup, [Year], Aau24H, Aau00D, Aau00N) VALUES ('DG1', 2026, 3853, 2945, 1966);
            """, cancellationToken: ct));
    }

    public sealed record ForecastRow
    {
        public long ForecastVersion { get; init; }

        public DateTime AnchorDate { get; init; }

        public short HorizonDays { get; init; }

        public decimal TotalKwh { get; init; }

        public required string UsageFactorSource { get; init; }

        public required string Daily { get; init; }

        public required string Inputs { get; init; }
    }
}
