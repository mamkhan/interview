using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.TestsShared;

// Defaults to the new meter of the real MM331 sample ESB_MM331_10900000331, serial 90003311: category RM106, location
// 07, exchanged meter reference 000000000090003311, put in place on 2026-09-16.
public sealed class MeterStatementBuilder
{
    private DateOnly _startDate = new(2026, 9, 16);
    private string? _meterCategoryCode = "RM106";
    private string? _meterLocationCode = "07";
    private string? _exchangedMeterReference = "000000000090003311";

    public MeterStatementBuilder StartingOn(DateOnly startDate)
    {
        _startDate = startDate;
        return this;
    }

    public MeterStatementBuilder WithDetails(string? meterCategoryCode, string? meterLocationCode, string? exchangedMeterReference)
    {
        _meterCategoryCode = meterCategoryCode;
        _meterLocationCode = meterLocationCode;
        _exchangedMeterReference = exchangedMeterReference;
        return this;
    }

    public Dated<MeterDetails> Build() => new(_startDate, new MeterDetails(_meterCategoryCode, _meterLocationCode, _exchangedMeterReference));
}
