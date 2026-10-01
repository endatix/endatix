using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Jobs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Subscribes the telemetry pipeline to Endatix's own meters and activity sources. Each is subscribed whether or not
/// the module behind it is on: a meter or source nothing creates exports nothing.
/// </summary>
internal static class EndatixTelemetrySources
{
    public static MeterProviderBuilder AddEndatixMeters(this MeterProviderBuilder metrics) =>
        metrics.AddMeter(JobsModule.MeterName, OutboxTelemetry.SourceName);

    // A job runs as a child of the request that enqueued it, and the relay's fan-out as a child of the request that
    // raised the event, so these sources are what joins the two sides of the queue.
    public static TracerProviderBuilder AddEndatixSources(this TracerProviderBuilder tracing) =>
        tracing.AddSource(JobsModule.ActivitySourceName, OutboxTelemetry.SourceName);
}
