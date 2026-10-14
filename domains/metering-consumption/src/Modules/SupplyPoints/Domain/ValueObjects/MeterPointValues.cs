using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// The meter point's codes and the networks reference number of the message that stated them, each a value of its own.
public sealed record MeterPointValues(string? MeterConfigurationCode, string? LoadProfileCode, string? DuosGroup,
    string? MeterPointStatusCode, string? NetworksReferenceNumber) : ICarriedValues<MeterPointValues>
{
    public static MeterPointValues None { get; } = new(null, null, null, null, null);

    // Not every message states all four codes (an MM300 has no meter configuration code, an MM301 no load profile or
    // status), so a code a message leaves out stays what it was.
    public MeterPointValues CarriedFrom(MeterPointValues? previous) => previous is null
        ? this
        : new MeterPointValues(
            MeterConfigurationCode ?? previous.MeterConfigurationCode,
            LoadProfileCode ?? previous.LoadProfileCode,
            DuosGroup ?? previous.DuosGroup,
            MeterPointStatusCode ?? previous.MeterPointStatusCode,
            NetworksReferenceNumber ?? previous.NetworksReferenceNumber);

    public MeterPointValues NotStatedIn(MeterPointValues other) => new(
        other.MeterConfigurationCode is null ? MeterConfigurationCode : null,
        other.LoadProfileCode is null ? LoadProfileCode : null,
        other.DuosGroup is null ? DuosGroup : null,
        other.MeterPointStatusCode is null ? MeterPointStatusCode : null,
        other.NetworksReferenceNumber is null ? NetworksReferenceNumber : null);
}
