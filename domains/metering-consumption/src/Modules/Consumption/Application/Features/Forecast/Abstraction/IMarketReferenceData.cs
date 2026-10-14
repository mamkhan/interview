using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;

public interface IMarketReferenceData
{
    // The load profile's hourly values from the first date to the last; the curve holds only the hours stored.
    Task<LoadProfileCurve> CurveAsync(string loadProfileCode, DateOnly first, DateOnly last, CancellationToken cancellationToken);

    Task<AnnualAverageUsage?> AnnualAverageAsync(string duosGroup, int year, CancellationToken cancellationToken);
}
