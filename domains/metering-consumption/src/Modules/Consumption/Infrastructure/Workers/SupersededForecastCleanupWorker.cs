using MeteringConsumption.Consumption.Application.Features.Forecast.Abstraction;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.Infrastructure.Workers;

// Deletes superseded forecast versions past their retention in batches, so one run never holds a long lock, then
// waits for the next interval. A failed run is logged and retried at the next interval. The clock is scoped, so
// each run takes it from a scope of its own; one nobody stamps reads the system clock.
public sealed class SupersededForecastCleanupWorker(
    IConsumptionForecastStore store,
    IOptions<ForecastOptions> options,
    IServiceScopeFactory scopes,
    ILogger<SupersededForecastCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            try
            {
                var cutOff = new DateTimeOffset(DateTime.Today).AddDays(-settings.SupersededRetentionDays);
                int deleted;
                do
                {
                    deleted = await store.DeleteSupersededAsync(cutOff, settings.CleanupBatchSize, stoppingToken);
                }
                while (deleted == settings.CleanupBatchSize);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Deleting superseded forecasts failed; retrying at the next interval");
            }

            try
            {
                await Task.Delay(settings.CleanupInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
