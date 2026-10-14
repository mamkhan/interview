using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.TestsShared;

// Defaults are the illustrative MM332 meter exchange the event contracts were agreed on: MCC01, load profile
// 01, DUoS group DG1, energised, effective 2026-09-21, from the file with the content hash below.
public sealed class MeterConfigurationStatementBuilder
{
    public const string DefaultSource = "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25";

    private string? _meterConfigurationCode = "MCC01";
    private string? _loadProfileCode = "01";
    private string? _duosGroup = "DG1";
    private string? _meterPointStatusCode = "E";
    private DateOnly _effectiveFrom = new(2026, 9, 21);
    private string _source = DefaultSource;
    private string _messageType = "332";

    public MeterConfigurationStatementBuilder WithMeterConfigurationCode(string? code)
    {
        _meterConfigurationCode = code;
        return this;
    }

    public MeterConfigurationStatementBuilder WithLoadProfileCode(string? code)
    {
        _loadProfileCode = code;
        return this;
    }

    public MeterConfigurationStatementBuilder WithDuosGroup(string? group)
    {
        _duosGroup = group;
        return this;
    }

    public MeterConfigurationStatementBuilder WithMeterPointStatusCode(string? code)
    {
        _meterPointStatusCode = code;
        return this;
    }

    public MeterConfigurationStatementBuilder EffectiveFrom(DateOnly date)
    {
        _effectiveFrom = date;
        return this;
    }

    public MeterConfigurationStatementBuilder FromSource(string contentHash, string messageType)
    {
        _source = contentHash;
        _messageType = messageType;
        return this;
    }

    public MeterConfigurationStatement Build() => new()
    {
        MeterConfigurationCode = _meterConfigurationCode,
        LoadProfileCode = _loadProfileCode,
        DuosGroup = _duosGroup,
        MeterPointStatusCode = _meterPointStatusCode,
        EffectiveFrom = _effectiveFrom,
        Source = _source,
        MessageType = _messageType,
    };
}
