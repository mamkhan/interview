using MeteringConsumption.SupplyPoints.Domain.Seedwork;

namespace MeteringConsumption.SupplyPoints.Domain.ValueObjects;

public sealed record MeterDetails(string? MeterCategoryCode, string? MeterLocationCode, string? ExchangedMeterReference)
    : ICarriedValues<MeterDetails>
{
    public static MeterDetails None { get; } = new(null, null, null);

    // ESB leaves the location out of a retained meter, and the category is optional everywhere, so what a message
    // leaves out stays what it was.
    public MeterDetails CarriedFrom(MeterDetails? previous) => previous is null
        ? this
        : new MeterDetails(
            MeterCategoryCode ?? previous.MeterCategoryCode,
            MeterLocationCode ?? previous.MeterLocationCode,
            ExchangedMeterReference ?? previous.ExchangedMeterReference);

    public MeterDetails NotStatedIn(MeterDetails other) => new(
        other.MeterCategoryCode is null ? MeterCategoryCode : null,
        other.MeterLocationCode is null ? MeterLocationCode : null,
        other.ExchangedMeterReference is null ? ExchangedMeterReference : null);
}
