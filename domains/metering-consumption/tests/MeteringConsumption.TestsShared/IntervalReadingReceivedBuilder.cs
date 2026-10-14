using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.TestsShared;

// Defaults are the persona of MeterDayReadingBuilder as Market Messaging announces it: MPRN 10001234567 on
// 2026-06-20, meter M123456, RRVN 1, from an MM343 with the hash below. The values are the operator's, as the
// event carries them: 48 half-hours of 0.5 KWT (average kW), which is 0.25 kWh each, starting at local midnight.
public sealed class IntervalReadingReceivedBuilder
{
    private static readonly DateTimeOffset FirstHalfHour = new(2026, 6, 20, 0, 0, 0, TimeSpan.FromHours(1));

    private string _mprn = MeterDayReadingBuilder.SupplyPointId;
    private string _contentHash = MeterDayReadingBuilder.ContentHash;
    private string _meterSerial = MeterDayReadingBuilder.DefaultMeterSerial;
    private string _market = "roi-esb";
    private string _correlationId = "5c2a4f1e-7d3b-4e8a-9f61-0b2c3d4e5f60";
    private DateOnly _readDate = MeterDayReadingBuilder.ReadDate;
    private int _rrvn = 1;
    private List<IntervalPeriod> _periods = HalfHours(0.5m, "VVAK");

    public IntervalReadingReceivedBuilder ForMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    // Moves the periods with the day, so they still start at its local midnight.
    public IntervalReadingReceivedBuilder OnDate(DateOnly readDate)
    {
        var days = readDate.DayNumber - _readDate.DayNumber;
        _periods = [.. _periods.Select(period => period with { Timestamp = period.Timestamp.AddDays(days) })];
        _readDate = readDate;
        return this;
    }

    public IntervalReadingReceivedBuilder FromFile(string contentHash)
    {
        _contentHash = contentHash;
        return this;
    }

    public IntervalReadingReceivedBuilder FromMeter(string meterSerial)
    {
        _meterSerial = meterSerial;
        return this;
    }

    public IntervalReadingReceivedBuilder FromMarket(string market)
    {
        _market = market;
        return this;
    }

    public IntervalReadingReceivedBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    public IntervalReadingReceivedBuilder WithRrvn(int rrvn)
    {
        _rrvn = rrvn;
        return this;
    }

    // In KWT, as the operator states it: 0.4 is 0.20 kWh over a half-hour.
    public IntervalReadingReceivedBuilder WithUniformValue(decimal value, string status = "VVAK")
    {
        _periods = HalfHours(value, status);
        return this;
    }

    public IntervalReadingReceivedBuilder WithPeriods(params IntervalPeriod[] periods)
    {
        _periods = [.. periods];
        return this;
    }

    public IntervalReadingReceived Build() => new()
    {
        EventId = Guid.Parse("a4f3c2d1-0b9e-5f8d-8c7b-6a5f4e3d2c1b"),
        PublishingContext = "metering-consumption.market-messaging",
        OccurredAtUtc = new DateTimeOffset(2026, 6, 21, 1, 51, 2, 330, TimeSpan.Zero),
        CorrelationId = _correlationId,
        MPRN = _mprn,
        MeterSerial = _meterSerial,
        ReadDate = _readDate,
        RRVN = _rrvn,
        MeteringIntervalMinutes = 30,
        UOMCode = "KWT",
        Periods = _periods,
        Provenance = new ReadingProvenance(
            _market,
            "343",
            new DateTimeOffset(2026, 6, 21, 1, 50, 10, 918, TimeSpan.Zero),
            $"{_market}/{_contentHash}.xml",
            _contentHash,
            "DSO_MIM_343_1782000000000_3800001.xml"),
    };

    public static IntervalPeriod HalfHour(int period, decimal value, string status = "VVAK") =>
        new(FirstHalfHour.AddMinutes(30 * (period - 1)), value, status);

    private static List<IntervalPeriod> HalfHours(decimal value, string status) =>
        [.. Enumerable.Range(1, 48).Select(period => HalfHour(period, value, status))];
}
