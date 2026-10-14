using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.TestsShared;
using MeteringConsumption.SupplyPoints.Domain.Aggregates;
using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using MeteringConsumption.SupplyPoints.Infrastructure.Persistence.Repositories;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.SupplyPoints.IntegrationTests;

// The repository on a database built the way the migrator builds one: each unit of work records statements through the
// domain's decision and saves what it names, and a fresh one reads the key back. Today is 2026-10-13 unless a test
// says otherwise; each test uses its own XPRN.
public sealed class WhenStoringASupplyPoint : IClassFixture<WhenStoringASupplyPoint.Database>, IAsyncDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly ServiceProvider _services;

    public WhenStoringASupplyPoint(Database database) =>
        _services = new ServiceCollection().AddUnitOfWork(database.ConnectionString).BuildServiceProvider();

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task KeepsANewSupplyPointWithItsMarketAndFirstRows()
    {
        var key = new MeterPointKey(new XPRN("10000000101"));
        await RecordAsync(key, new MeterPointStatementBuilder().Build());

        var facts = await FactsAsync(key, new DateOnly(2026, 3, 1));

        var stated = new Dated<MeterPointValues>(new DateOnly(2026, 3, 1), new MeterPointValues("MCC01", "01", "DG1", "E", "NRN-hash-mm105"));
        (await MarketOfAsync(key.Xprn)).Should().Be("roi-esb");
        facts.Current.Should().Equal(stated);
        facts.HistoryOnDate.Should().Be(stated);
    }

    // History for 24H: estimate 1 and actual 1 on 2026-01-01, estimate 2 on 2026-02-01, actual 3 on 2026-03-01, estimate
    // 4 on 2026-04-01, estimate 5 and actual 5 on 2026-05-01, actual 6 on 2026-06-01. Around 2026-03-15 the latest
    // estimate is 2026-02-01's and the latest actual 2026-03-01's; the first later estimate is 2026-04-01's and the first
    // later actual 2026-05-01's. No other row is read.
    [Fact]
    public async Task ReadsOnlyTheHistoryRowsThatGiveEachValueItsLatestAndNextStatement()
    {
        var key = new UsageFactorKey(new XPRN("10000000102"), "24H");
        foreach (var (month, estimated, actual) in new (int, decimal?, decimal?)[]
                 { (1, 1m, 1m), (2, 2m, null), (3, null, 3m), (4, 4m, null), (5, 5m, 5m), (6, null, 6m) })
        {
            await RecordAsync(key, new Dated<UsageFactorValues>(new DateOnly(2026, month, 1), new UsageFactorValues(estimated, actual)));
        }

        var facts = await FactsAsync(key, new DateOnly(2026, 3, 15));

        facts.HistoryOnDate.Should().BeNull();
        facts.StatedOnOrBefore.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 2, 1), new UsageFactorValues(2m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 3, 1), new UsageFactorValues(null, 3m)));
        facts.StatedAfter.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 4, 1), new UsageFactorValues(4m, null)),
            new Dated<UsageFactorValues>(new DateOnly(2026, 5, 1), new UsageFactorValues(5m, 5m)));
        facts.Current.Should().Equal(new Dated<UsageFactorValues>(new DateOnly(2026, 6, 1), new UsageFactorValues(5m, 6m)));
    }

    // A (MM105) from 2026-03-01, then a correction for 2026-03-01 stating only MCC05 under NRN-hash-correction.
    [Fact]
    public async Task OverwritesTheHistoryRowOfARepeatedDateAndUpdatesTodaysRow()
    {
        var key = new MeterPointKey(new XPRN("10000000103"));
        await RecordAsync(key, new MeterPointStatementBuilder().Build());
        await RecordAsync(key, new MeterPointStatementBuilder().WithCodes("MCC05", null, null, null)
            .WithNetworksReferenceNumber("NRN-hash-correction").Build());

        var facts = await FactsAsync(key, new DateOnly(2026, 3, 1));

        var corrected = new Dated<MeterPointValues>(new DateOnly(2026, 3, 1), new MeterPointValues("MCC05", "01", "DG1", "E", "NRN-hash-correction"));
        facts.HistoryOnDate.Should().Be(corrected);
        facts.Current.Should().Equal(corrected);
    }

    // The 24H estimate 4127.5 from 2026-03-01, then the actual 3980.25 from 2026-06-15: today's row is 2026-06-15's.
    [Fact]
    public async Task DeletesTheCurrentRowsBeforeTodaysRow()
    {
        var key = new UsageFactorKey(new XPRN("10000000104"), "24H");
        await RecordAsync(key, new SupplyPointUsageFactorStatementBuilder().Build());
        await RecordAsync(key, new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 6, 15)).WithValues(null, 3980.25m).Build());

        var facts = await FactsAsync(key, new DateOnly(2026, 6, 15));

        facts.Current.Should().Equal(new Dated<UsageFactorValues>(new DateOnly(2026, 6, 15), new UsageFactorValues(4127.5m, 3980.25m)));
        facts.StatedOnOrBefore.Select(row => row.EffectiveDate).Should().Equal(new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 15));
    }

    // ESB states usage factors with up to eight decimals; the repository's parameters must not round any away.
    [Fact]
    public async Task KeepsEveryDecimalOfAUsageFactor()
    {
        var key = new UsageFactorKey(new XPRN("10000000105"), "24H");
        var statement = new SupplyPointUsageFactorStatementBuilder().WithValues(estimated: 4127.12345678m, actual: 0.00000001m).Build();
        await RecordAsync(key, statement);

        (await FactsAsync(key, statement.EffectiveDate)).Current.Should().Equal(statement);
    }

    // Meter 90000320 (RM107, location 07) with register 001 and its read 412 (type A, reason 04, status V, previous read
    // 2026-09-01), and meter 90003311's channel 70 in KWT every 30 minutes, all from 2026-09-30.
    [Fact]
    public async Task KeepsAMeterARegisterWithItsWholeReadAndAChannel()
    {
        var xprn = new XPRN("10000000106");
        var meter = new MeterStatementBuilder().StartingOn(new DateOnly(2026, 9, 30)).WithDetails("RM107", "07", null).Build();
        var register = new RegisterStatementBuilder().WithRead(new RegisterRead(412m, "A", "04", "V", new DateOnly(2026, 9, 1))).Build();
        var channel = new RegisterStatementBuilder().AsAChannel("70", "KWT", 30).StartingOn(new DateOnly(2026, 9, 30)).Build();
        await RecordAsync(new MeterKey(xprn, "90000320"), meter);
        await RecordAsync(new RegisterKey(xprn, new RegisterId("90000320", "001")), register);
        await RecordAsync(new RegisterKey(xprn, new RegisterId("90003311", "70:KWT")), channel);

        (await FactsAsync(new MeterKey(xprn, "90000320"), meter.EffectiveDate)).Current.Should().Equal(meter);
        (await FactsAsync(new RegisterKey(xprn, new RegisterId("90000320", "001")), register.EffectiveDate)).Current.Should().Equal(register);
        (await FactsAsync(new RegisterKey(xprn, new RegisterId("90003311", "70:KWT")), channel.EffectiveDate)).Current.Should().Equal(channel);
    }

    // A (MM105) from 2026-03-01, the 00D estimate 4127.5 from 2026-03-01 and meter 90003311 from 2026-09-16 stored from
    // messages, then a reload stating MCC02, 02, DG2 from 2026-08-03 and the 24H estimate 4127.5 from 2026-08-03 and actual
    // 3980.25 from 2026-09-01: only the reload's meter point and usage factor rows remain, and the meter stays.
    [Fact]
    public async Task ReplacesTheMeterPointAndUsageFactorRowsOfAReload()
    {
        var xprn = new XPRN("10000000107");
        await RecordAsync(new MeterPointKey(xprn), new MeterPointStatementBuilder().Build());
        await RecordAsync(new UsageFactorKey(xprn, "00D"), new SupplyPointUsageFactorStatementBuilder().Build());
        await RecordAsync(new MeterKey(xprn, "90003311"), new MeterStatementBuilder().Build());

        await InUnitOfWorkAsync(repository => repository.SaveReloadAsync(xprn, SupplyPoint.Reload(
            new MeterPointStatementBuilder().StartingOn(new DateOnly(2026, 8, 3)).WithCodes("MCC02", "02", "DG2", null)
                .WithNetworksReferenceNumber(null).Build(),
            [
                ("24H", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 8, 3)).WithValues(4127.5m, null).Build()),
                ("24H", new SupplyPointUsageFactorStatementBuilder().StartingOn(new DateOnly(2026, 9, 1)).WithValues(null, 3980.25m).Build()),
            ],
            Today), Token));

        var meterPoint = await FactsAsync(new MeterPointKey(xprn), new DateOnly(2026, 3, 1));
        meterPoint.Current.Should().Equal(
            new Dated<MeterPointValues>(new DateOnly(2026, 8, 3), new MeterPointValues("MCC02", "02", "DG2", null, null)));
        meterPoint.StatedOnOrBefore.Should().BeEmpty();
        var dayFactor = await FactsAsync(new UsageFactorKey(xprn, "00D"), new DateOnly(2026, 3, 1));
        dayFactor.Current.Should().BeEmpty();
        dayFactor.StatedOnOrBefore.Should().BeEmpty();
        (await FactsAsync(new UsageFactorKey(xprn, "24H"), new DateOnly(2026, 9, 1))).Current.Should().Equal(
            new Dated<UsageFactorValues>(new DateOnly(2026, 9, 1), new UsageFactorValues(4127.5m, 3980.25m)));
        (await FactsAsync(new MeterKey(xprn, "90003311"), new DateOnly(2026, 9, 16))).Current.Should().ContainSingle();
    }

    [Fact]
    public async Task FindsNothingForASupplyPointNoMessageReached()
    {
        (await MarketOfAsync(new XPRN("10000000199"))).Should().BeNull();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task RecordAsync<TValues>(ConfigurationKey<TValues> key, Dated<TValues> statement)
        where TValues : class, ICarriedValues<TValues> => InUnitOfWorkAsync(async repository =>
    {
        if (await repository.FindAsync(key.Xprn, Token) is null)
        {
            await repository.AddAsync(SupplyPoint.New(key.Xprn, "roi-esb"), Token);
        }

        var recorded = Recording.Decide(statement, Today, await repository.FactsAsync(key, statement.EffectiveDate, Token));
        await repository.SaveAsync(key, recorded, Token);
    });

    private async Task<KeyFacts<TValues>> FactsAsync<TValues>(ConfigurationKey<TValues> key, DateOnly effectiveDate)
        where TValues : class, ICarriedValues<TValues>
    {
        KeyFacts<TValues>? facts = null;
        await InUnitOfWorkAsync(async repository => facts = await repository.FactsAsync(key, effectiveDate, Token));
        return facts!;
    }

    private async Task<string?> MarketOfAsync(XPRN xprn)
    {
        SupplyPoint? found = null;
        await InUnitOfWorkAsync(async repository => found = await repository.FindAsync(xprn, Token));
        return found?.Market;
    }

    private async Task InUnitOfWorkAsync(Func<SupplyPointRepository, Task> work)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(Token);
        await work(new SupplyPointRepository(unitOfWork));
        await unitOfWork.CommitAsync(Token);
    }

    public sealed class Database()
        : ClassDatabase(Suite.Id, nameof(WhenStoringASupplyPoint), SupplyPointSchema.ApplySupplyPointSchemaAsync);
}
