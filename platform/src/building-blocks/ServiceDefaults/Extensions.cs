using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Azure.Messaging.ServiceBus keeps its ActivitySource behind this switch; without it the SDK emits
        // no bus spans at all and a file's trace breaks at every hop.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // The Azure SDK logs every Service Bus receive/complete/renew at Information, which drowns Log
        // Analytics under load; Warning keeps errors and drops the per-message chatter. Overridable per
        // service through the usual Logging:LogLevel configuration.
        builder.Logging.AddFilter("Azure", LogLevel.Warning);

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        builder.Services.Configure<ServiceDiscoveryOptions>(options => { options.AllowedSchemes = ["https"]; });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<PlatformExceptionHandler>();

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(BuildingBlocks.Messaging.MessagingTelemetry.MeterName);
            })
            .WithTracing(tracing =>
            {
                // The Azure SDK emits the producer and consumer spans that carry W3C trace context across
                // a Service Bus hop; without the source registered the trace stops at the publish and every
                // consumer looks like an unrelated root.
                tracing.AddSource("Azure.Messaging.ServiceBus")
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(t =>
                        t.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Deployed hosts export to Application Insights; locally the Aspire dashboard's OTLP endpoint is
        // set instead and wins, so a host never double-exports the same telemetry.
        if (!useOtlpExporter && !string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.UseExceptionHandler();

        // Mapped in all environments: deployed reachability is internal-only and the probes need a target;
        // the endpoints leak nothing beyond status.
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
        });

        return app;
    }
}
