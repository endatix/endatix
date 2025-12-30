using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Endatix.Infrastructure.Logging;

/// <summary>
/// Provides methods to configure OpenTelemetry within the application.
/// </summary>
public static class OpenTelemetryConfigurator
{
    /// <summary>
    /// Configures OpenTelemetry logging with the application's configuration.
    /// </summary>
    /// <param name="loggingBuilder">The logging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configureActions">Optional action to configure OpenTelemetry.</param>
    public static void ConfigureOpenTelemetry(
        this ILoggingBuilder loggingBuilder,
        IConfiguration configuration,
        Action<OpenTelemetryLoggerOptions>? configureActions = null)
    {
        loggingBuilder.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            var serviceName = configuration["ServiceName"] ?? "endatix-service";
            options.SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName));


            var azureMonitorConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(azureMonitorConnectionString))
            {
                options.AddAzureMonitorLogExporter(o => o.ConnectionString = azureMonitorConnectionString);
            }
            else
            {
                options.AddOtlpExporter();
            }

            configureActions?.Invoke(options);
        });

        loggingBuilder.Services
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
            {
                metrics
                   .AddAspNetCoreInstrumentation()
                   .AddHttpClientInstrumentation()
                   .AddView("http.server.request.duration",
                       new ExplicitBucketHistogramConfiguration
                       {
                           Boundaries =
                               [0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
                       }
                    )
                    .AddOtlpExporter();
            })
                .WithTracing(tracing =>
                {
                    tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
                });
    }
}