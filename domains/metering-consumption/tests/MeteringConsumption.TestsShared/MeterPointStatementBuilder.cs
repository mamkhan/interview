using MeteringConsumption.SupplyPoints.Domain.Seedwork;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.TestsShared;

// Defaults to row A of the Diagrams 4 example: an MM105 stating MCC01, load profile 01, DUoS group DG1 and status
// E from 2026-03-01.
public sealed class MeterPointStatementBuilder
{
    private DateOnly _startDate = new(2026, 3, 1);
    private string? _meterConfigurationCode = "MCC01";
    private string? _loadProfileCode = "01";
    private string? _duosGroup = "DG1";
    private string? _meterPointStatusCode = "E";
    private string? _networksReferenceNumber = "NRN-hash-mm105";

    public MeterPointStatementBuilder StartingOn(DateOnly startDate)
    {
        _startDate = startDate;
        return this;
    }

    public MeterPointStatementBuilder WithCodes(string? meterConfigurationCode, string? loadProfileCode, string? duosGroup,
        string? meterPointStatusCode)
    {
        _meterConfigurationCode = meterConfigurationCode;
        _loadProfileCode = loadProfileCode;
        _duosGroup = duosGroup;
        _meterPointStatusCode = meterPointStatusCode;
        return this;
    }

    public MeterPointStatementBuilder WithNetworksReferenceNumber(string? networksReferenceNumber)
    {
        _networksReferenceNumber = networksReferenceNumber;
        return this;
    }

    public Dated<MeterPointValues> Build() => new(
        _startDate,
        new MeterPointValues(_meterConfigurationCode, _loadProfileCode, _duosGroup, _meterPointStatusCode, _networksReferenceNumber));

}
