using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using BuildingBlocks.Seedwork;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using Microsoft.Data.SqlClient;

namespace MeteringConsumption.SupplyPoints.UnitTests.Store;

// Holds each key's history and current rows the way the tables do: a save writes only what its decision names, so a
// handler that decides without saving leaves nothing here. Store and Reload share it, as they share the tables.
internal sealed class FakeSupplyPointRepository : ISupplyPointRepository, ISupplyPointReloadRepository
{
    private readonly Dictionary<string, string> _markets = [];
    private readonly Dictionary<object, object> _keys = [];

    public string? MarketOf(string xprn) => _markets.GetValueOrDefault(xprn);

    public KeyRows<MeterPointValues> MeterPoint(string xprn) => RowsOf(new MeterPointKey(new XPRN(xprn)));

    public KeyRows<UsageFactorValues> UsageFactor(string xprn, string timeslotCode) =>
        RowsOf(new UsageFactorKey(new XPRN(xprn), timeslotCode));

    public KeyRows<MeterDetails> Meter(string xprn, string serialNumber) => RowsOf(new MeterKey(new XPRN(xprn), serialNumber));

    public KeyRows<RegisterValues> Register(string xprn, string serialNumber, string key) =>
        RowsOf(new RegisterKey(new XPRN(xprn), new RegisterId(serialNumber, key)));

    public Task<SupplyPoint?> FindAsync(XPRN xprn, CancellationToken cancellationToken) =>
        Task.FromResult(_markets.TryGetValue(xprn.Value, out var market) ? SupplyPoint.Restore(xprn, market) : null);

    public Task AddAsync(SupplyPoint supplyPoint, CancellationToken cancellationToken)
    {
        _markets.Add(supplyPoint.Id.Value, supplyPoint.Market);
        return Task.CompletedTask;
    }

    public Task<KeyFacts<TValues>> FactsAsync<TValues>(ConfigurationKey<TValues> key, DateOnly effectiveDate,
        CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues> =>
        Task.FromResult(RowsOf(key).FactsAround(effectiveDate));

    public Task SaveAsync<TValues>(ConfigurationKey<TValues> key, Recorded<TValues> recorded, CancellationToken cancellationToken)
        where TValues : class, ICarriedValues<TValues>
    {
        RowsOf(key).Apply(recorded);
        return Task.CompletedTask;
    }

    public Task SaveReloadAsync(XPRN xprn, ReloadOutcome outcome, CancellationToken cancellationToken)
    {
        foreach (var replaced in _keys.Keys.Where(key => (key is MeterPointKey meterPoint && meterPoint.Xprn == xprn)
                     || (key is UsageFactorKey usageFactor && usageFactor.Xprn == xprn)).ToList())
        {
            _keys.Remove(replaced);
        }

        _keys[new MeterPointKey(xprn)] = outcome.MeterPoint;
        foreach (var (timeslotCode, rows) in outcome.UsageFactors)
        {
            _keys[new UsageFactorKey(xprn, timeslotCode)] = rows;
        }

        return Task.CompletedTask;
    }

    private KeyRows<TValues> RowsOf<TValues>(ConfigurationKey<TValues> key)
        where TValues : class, ICarriedValues<TValues>
    {
        if (!_keys.TryGetValue(key, out var rows))
        {
            rows = new KeyRows<TValues>();
            _keys.Add(key, rows);
        }

        return (KeyRows<TValues>)rows;
    }
}

// Records what the command runner asks of it. The inbox remembers a marker only once the work commits.
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly HashSet<(string Consumer, string MessageId)> _processed = [];
    private (string Consumer, string MessageId)? _pending;

    public int Begun { get; private set; }

    public int Commits { get; private set; }

    public IReadOnlyCollection<(string Consumer, string MessageId)> Processed => _processed;

    public SqlConnection Connection => throw new NotSupportedException("The fake unit of work has no connection.");

    public SqlTransaction? Transaction => null;

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        Begun++;
        return Task.CompletedTask;
    }

    public void Track(IAggregateRoot aggregate)
    {
    }

    public Task AddToOutboxAsync<TEvent>(TEvent contractEvent, EventMetadata metadata, CancellationToken cancellationToken)
        where TEvent : class => throw new NotSupportedException("Store publishes nothing.");

    public Task<bool> TryMarkProcessedAsync(string consumer, string messageId, CancellationToken cancellationToken)
    {
        if (_processed.Contains((consumer, messageId)))
        {
            return Task.FromResult(false);
        }

        _pending = (consumer, messageId);
        return Task.FromResult(true);
    }

    public Task<string?> TryBeginClientRequestAsync(Guid requestId, string commandName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Store takes no client requests.");

    public Task CompleteClientRequestAsync(Guid requestId, string resultJson, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Store takes no client requests.");

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_pending is { } marker)
        {
            _processed.Add(marker);
            _pending = null;
        }

        Commits++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
