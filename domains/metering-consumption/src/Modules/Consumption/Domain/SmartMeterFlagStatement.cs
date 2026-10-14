namespace MeteringConsumption.Consumption.Domain;

public sealed record SmartMeterFlagStatement(bool IsNsmp, DateTimeOffset AsOf, string Source)
{
    public static SmartMeterFlagStatement Of(bool isNsmp, DateTimeOffset asOf, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ProfileStatementRejectedException("The smart meter flag has no source.");
        }

        return new(isNsmp, asOf, source);
    }
}
