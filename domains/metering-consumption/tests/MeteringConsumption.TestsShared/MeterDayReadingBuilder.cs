using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Defaults are the proof of concept's ledger persona: MPRN 10001234567 on 2026-06-20, meter M123456, RRVN 1,
// 48 half-hours of 0.25 kWh, all VVAK, from a file with the content hash below.
public sealed class MeterDayReadingBuilder
{
    public const string SupplyPointId = "10001234567";
    public const string DefaultMeterSerial = "M123456";
    public const string ExchangedMeterSerial = "M999888";
    public const string ContentHash = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";

    public static readonly DateOnly ReadDate = new(2026, 6, 20);

    public static MeterDay MeterDay { get; } = new(SupplyPointId, ReadDate);

    private MeterDay _meterDay = MeterDay;
    private string _contentHash = ContentHash;
    private string _meterSerial = DefaultMeterSerial;
    private int _rrvn = 1;
    private int _meteringIntervalMinutes = 30;
    private string _uomCode = "KWT";
    private List<decimal> _kwh = [.. Enumerable.Repeat(0.25m, 48)];
    private List<string>? _statuses;
    private string _uniformStatus = "VVAK";

    public MeterDayReadingBuilder ForDay(MeterDay meterDay)
    {
        _meterDay = meterDay;
        return this;
    }

    public MeterDayReadingBuilder WithRrvn(int rrvn)
    {
        _rrvn = rrvn;
        return this;
    }

    public MeterDayReadingBuilder FromFile(string contentHash)
    {
        _contentHash = contentHash;
        return this;
    }

    public MeterDayReadingBuilder FromMeter(string meterSerial)
    {
        _meterSerial = meterSerial;
        return this;
    }

    public MeterDayReadingBuilder WithUniformKwh(decimal kwh, int periods = 48)
    {
        _kwh = [.. Enumerable.Repeat(kwh, periods)];
        return this;
    }

    public MeterDayReadingBuilder WithPeriodKwh(params decimal[] kwh)
    {
        _kwh = [.. kwh];
        return this;
    }

    public MeterDayReadingBuilder WithStatus(string status)
    {
        _uniformStatus = status;
        _statuses = null;
        return this;
    }

    public MeterDayReadingBuilder WithPeriodStatuses(params string[] statuses)
    {
        _statuses = [.. statuses];
        return this;
    }

    public MeterDayReadingBuilder WithNoPeriods() => WithPeriodKwh();

    public MeterDayReading Build() => new()
    {
        ReadingId = $"{_contentHash}:{_meterDay.SupplyPointId}:{_meterDay.ReadDate:yyyy-MM-dd}:{_rrvn}",
        MeterSerial = _meterSerial,
        Rrvn = _rrvn,
        MeteringIntervalMinutes = _meteringIntervalMinutes,
        UomCode = _uomCode,
        Periods = [.. _kwh.Select((kwh, index) => new EntryPeriod(index + 1, kwh, _statuses?[index] ?? _uniformStatus))],
    };
}
