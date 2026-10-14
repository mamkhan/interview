using MeteringConsumption.Contracts.Events.DTO;
using MeteringConsumption.Contracts.Events.MarketMessaging;

namespace MeteringConsumption.TestsShared;

// Defaults are the agreed MeterConfigurationReceived example: an illustrative MM332 meter exchange for MPRN
// 10012345678, MCC01, load profile 01, DUoS group DG1, energised, effective 2026-09-21.
public sealed class MeterConfigurationReceivedBuilder
{
    public const string ContentHash = "801a3bd1844770b09b79963fd11727a966777b52cc7b63631042c594d545fc25";

    private string _mprn = "10012345678";
    private string? _meterConfigurationCode = "MCC01";
    private string? _loadProfileCode = "01";
    private string? _duosGroup = "DG1";
    private string? _meterPointStatusCode = "E";
    private DateOnly? _effectiveFrom = new(2026, 9, 21);
    private DateOnly? _readDate;
    private string _contentHash = ContentHash;
    private string _messageType = "332";
    private string? _networksReferenceNumber = "000000123456";
    private bool? _isNsmp;
    private DateTimeOffset? _asOf;
    private string _publishingContext = "metering-consumption.market-messaging";
    private DateTimeOffset _marketTimestamp = new(2026, 9, 27, 21, 14, 3, 112, TimeSpan.Zero);
    private string _blobPath = $"roi-esb/{ContentHash}.xml";
    private string _receivedFileName = "DSO_MIM_332_1790000000000_4100001.xml";

    public MeterConfigurationReceivedBuilder ForMprn(string mprn)
    {
        _mprn = mprn;
        return this;
    }

    public MeterConfigurationReceivedBuilder WithMeterConfigurationCode(string? code)
    {
        _meterConfigurationCode = code;
        return this;
    }

    public MeterConfigurationReceivedBuilder WithLoadProfileCode(string? code)
    {
        _loadProfileCode = code;
        return this;
    }

    public MeterConfigurationReceivedBuilder EffectiveFrom(DateOnly date)
    {
        _effectiveFrom = date;
        return this;
    }

    // The shape a 320 sends: no effective date of its own, only the change-of-supplier reading's date.
    public MeterConfigurationReceivedBuilder ReadOn(DateOnly date)
    {
        _effectiveFrom = null;
        _readDate = date;
        return this;
    }

    public MeterConfigurationReceivedBuilder WithNoDates()
    {
        _effectiveFrom = null;
        _readDate = null;
        return this;
    }

    public MeterConfigurationReceivedBuilder FromFile(string contentHash, string messageType)
    {
        _contentHash = contentHash;
        _messageType = messageType;
        _blobPath = $"roi-esb/{contentHash}.xml";
        return this;
    }

    public MeterConfigurationReceivedBuilder StatingTheSmartMeterFlag(bool isNsmp, DateTimeOffset asOf)
    {
        _isNsmp = isNsmp;
        _asOf = asOf;
        return this;
    }

    public MeterConfigurationReceivedBuilder WithoutAsOf()
    {
        _asOf = null;
        return this;
    }

    // The V1 reader's shape on its run of 2026-10-07 at 04:00 UTC: the smart meter flag and nothing else, from no
    // sealed file, so every optional field is empty and the blob path and file name are blank.
    public MeterConfigurationReceivedBuilder AsTheV1ReaderStatesIt(bool isNsmp)
    {
        var asOf = new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);
        _meterConfigurationCode = null;
        _loadProfileCode = null;
        _duosGroup = null;
        _meterPointStatusCode = null;
        _effectiveFrom = null;
        _readDate = null;
        _networksReferenceNumber = null;
        _isNsmp = isNsmp;
        _asOf = asOf;
        _publishingContext = "platform.v1-reader";
        _marketTimestamp = asOf;
        _contentHash = $"v1-reader:2026-10-07:{(isNsmp ? 1 : 0)}";
        _messageType = "V1";
        _blobPath = "";
        _receivedFileName = "";
        return this;
    }

    public MeterConfigurationReceived Build() => new()
    {
        EventId = Guid.Parse("e1407479-3136-56c0-9908-bb02fb0339e2"),
        PublishingContext = _publishingContext,
        OccurredAtUtc = new DateTimeOffset(2026, 9, 28, 2, 50, 14, 201, TimeSpan.Zero),
        CorrelationId = "71039e6c-de90-565a-8cd7-89b263271615",
        AsOf = _asOf,
        MPRN = _mprn,
        MeterConfigurationCode = _meterConfigurationCode,
        LoadProfileCode = _loadProfileCode,
        DuosGroup = _duosGroup,
        MeterPointStatusCode = _meterPointStatusCode,
        EffectiveFromDate = _effectiveFrom,
        ReadDate = _readDate,
        NetworksReferenceNumber = _networksReferenceNumber,
        IsNsmp = _isNsmp,
        Provenance = new ReadingProvenance(
            "roi-esb",
            _messageType,
            _marketTimestamp,
            _blobPath,
            _contentHash,
            _receivedFileName),
    };
}
