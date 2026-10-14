using Azure.Messaging.ServiceBus;
using Dapper;
using Growth.Contracts;
using Growth.Contracts.Events;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace MeteringConsumption.AcceptanceTests.Consumption;

// Publishes what the V1 reader publishes into the running environment the way it does: the MPRN as session id, and a
// message id that names the event's source and MPRN. Then reads the supply point's Profile row back. Market Messaging's
// events reach the Profile from real files instead (WhenEsbSendsAnMm332ForANewSupplyPoint).
internal static class ProfileEvents
{
    // The event travels through Service Bus to a host that may be scaling up, so the suite waits for the row
    // rather than for a fixed interval, and gives up loudly.
    private static readonly TimeSpan RowDeadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan BetweenPolls = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A new MPRN on every run keeps the row new, so it holds only what this run published.
    public static string NewMprn()
    {
        var identity = Guid.NewGuid();
        return $"1{BitConverter.ToUInt64(identity.ToByteArray()) % 10_000_000_000:D10}";
    }

    public static Task PublishAsync(TargetEnvironment environment, MeterConfigurationReceived @event, CancellationToken ct) =>
        PublishAsync(environment, MeteringConsumptionTopology.Topics.MeterConfigurationReceived, @event, @event.MPRN,
            $"{@event.Provenance.ContentHash}:{@event.MPRN}:{nameof(MeterConfigurationReceived)}", ct);

    // On Growth's topic, as the V1 reader publishes it each night.
    public static Task PublishAsync(TargetEnvironment environment, TariffResolved @event, CancellationToken ct) =>
        PublishAsync(environment, GrowthTopology.Topics.TariffResolved, @event, @event.MPRN,
            $"{GrowthTopology.Topics.TariffResolved}:{@event.MPRN}:{@event.AsOf:yyyy-MM-dd}", ct);

    // Two events fill two groups of one row, each in its own transaction and in either order, so the row is
    // read until both are there.
    public static async Task<TRow> WaitForTheRowAsync<TRow>(TargetEnvironment environment, string sql, string mprn,
        Func<TRow, bool> isComplete, CancellationToken ct) where TRow : class
    {
        var giveUpAfter = DateTimeOffset.UtcNow + RowDeadline;
        TRow? row = null;
        while (DateTimeOffset.UtcNow < giveUpAfter)
        {
            await using var connection = new SqlConnection(environment.SqlConnectionString);
            row = await connection.QuerySingleOrDefaultAsync<TRow>(
                new CommandDefinition(sql, new { Mprn = mprn }, cancellationToken: ct));
            if (row is not null && isComplete(row))
            {
                return row;
            }

            await Task.Delay(BetweenPolls, ct);
        }

        throw new InvalidOperationException(row is null
            ? $"No profile row for {mprn} appeared within {RowDeadline}, so no event reached the Profile feature."
            : $"The profile row for {mprn} was still incomplete after {RowDeadline}: {row}.");
    }

    private static async Task PublishAsync<TEvent>(TargetEnvironment environment, string topic, TEvent @event,
        string mprn, string messageId, CancellationToken ct)
    {
        await using var bus = environment.ServiceBus();
        await using var sender = bus.CreateSender(topic);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(@event, Json))
        {
            MessageId = messageId,
            SessionId = mprn,
            ContentType = "application/json",
        }, ct);
    }
}
