using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.DTO;

// The Profile's tariff values and smart meter flag for a supply point, with when each group was read and who stated it.
public sealed record TariffInEffect(
    TariffFacts Facts,
    DateTimeOffset? TariffAsOf,
    string? TariffSource,
    DateTimeOffset? SmartMeterFlagAsOf,
    string? SmartMeterFlagSource);
