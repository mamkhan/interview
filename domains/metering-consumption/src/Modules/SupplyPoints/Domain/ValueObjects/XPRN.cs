using MeteringConsumption.SupplyPoints.Domain.Exceptions;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

// The supply point's reference number whatever the fuel: the MPRN for electricity, the GPRN for gas. At most 11
// characters, the length of ESB's MPRN.
public sealed record XPRN
{
    public XPRN(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 11)
        {
            throw new SupplyPointRejectedException($"'{value}' is not a supply point reference.");
        }

        Value = value;
    }

    public string Value { get; }
}
