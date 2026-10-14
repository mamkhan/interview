using System.Diagnostics.Metrics;
using MeteringConsumption.Consumption.Domain;

namespace MeteringConsumption.Consumption.Application.Features.Forecast;

public static class ForecastTelemetry
{
    private static readonly Meter Meter = new(ConsumptionMeter.Name);

    private static readonly Counter<long> Computed = Meter.CreateCounter<long>(
        "consumption.forecast.computed", "{forecast}", "Forecasts stored and announced from a usage factor.");

    // Non-zero means meters forecast as zeros for want of any usable usage factor, as V1 does.
    private static readonly Counter<long> ZeroForecasts = Meter.CreateCounter<long>(
        "consumption.forecast.zero_forecasts", "{forecast}", "Forecasts of zeros stored for want of a usage factor.");

    private static readonly Counter<long> NotForecast = Meter.CreateCounter<long>(
        "consumption.forecast.not_forecast", "{reading}", "Recorded readings that gave no forecast, by reason.");

    private static readonly Histogram<double> Lag = Meter.CreateHistogram<double>(
        "consumption.forecast.lag", "s", "Time from the entry being posted to its forecast being stored.");

    public static void RecordComputed(TimeSpan lag)
    {
        Computed.Add(1);
        Lag.Record(lag.TotalSeconds);
    }

    public static void RecordZeroForecast(TimeSpan lag)
    {
        ZeroForecasts.Add(1);
        Lag.Record(lag.TotalSeconds);
    }

    public static void RecordNotForecast(NotForecastReason reason) =>
        NotForecast.Add(1, new KeyValuePair<string, object?>("reason", reason.ToString()));
}
