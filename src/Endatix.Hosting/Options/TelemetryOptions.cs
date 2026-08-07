namespace Endatix.Hosting.Options;

/// <summary>
/// Options for Endatix OpenTelemetry, bound from the <c>Endatix:Telemetry</c> configuration section.
/// </summary>
/// <remarks>
/// Every value here is a <em>fallback</em>. The standard <c>OTEL_*</c> environment variables are
/// authoritative and win whenever they are set, per the OpenTelemetry specification — so a host that
/// configures telemetry the standard way never has to know these keys exist.
/// </remarks>
public class TelemetryOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Endatix:Telemetry";

    /// <summary>
    /// Logical service name. Falls back to the entry assembly name.
    /// Overridden by <c>OTEL_SERVICE_NAME</c>.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Service version reported as the <c>service.version</c> resource attribute.
    /// Overridden by <c>OTEL_SERVICE_VERSION</c>.
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Additional resource attributes attached to every signal.
    /// Merged with, and overridden by, <c>OTEL_RESOURCE_ATTRIBUTES</c>.
    /// </summary>
    public IDictionary<string, string> ResourceAttributes { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// OTLP exporter settings. The exporter is registered only when an endpoint resolves.
    /// </summary>
    public OtlpOptions Otlp { get; set; } = new();

    /// <summary>
    /// Per-source instrumentation toggles. All are enabled by default.
    /// </summary>
    public InstrumentationOptions Instrumentation { get; set; } = new();

    /// <summary>
    /// Trace sampling settings.
    /// </summary>
    public TracesOptions Traces { get; set; } = new();
}

/// <summary>
/// OTLP exporter settings.
/// </summary>
public class OtlpOptions
{
    /// <summary>
    /// Collector endpoint, for example <c>http://collector:4317</c>. Overridden by
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>. When neither is set, no exporter is registered.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Wire protocol: <c>grpc</c> (default) or <c>http/protobuf</c>.
    /// Overridden by <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>.
    /// </summary>
    public string? Protocol { get; set; }
}

/// <summary>
/// Per-source instrumentation toggles.
/// </summary>
public class InstrumentationOptions
{
    /// <summary>
    /// Inbound HTTP request traces and metrics. Enabled by default.
    /// </summary>
    public bool AspNetCore { get; set; } = true;

    /// <summary>
    /// Outbound <see cref="System.Net.Http.HttpClient"/> traces and metrics — this is what makes
    /// webhook delivery visible. Enabled by default.
    /// </summary>
    public bool HttpClient { get; set; } = true;

    /// <summary>
    /// .NET runtime metrics (GC, thread pool, allocation). Enabled by default.
    /// </summary>
    public bool Runtime { get; set; } = true;
}

/// <summary>
/// Trace sampling settings.
/// </summary>
public class TracesOptions
{
    /// <summary>
    /// Head-sampling ratio between 0.0 and 1.0. Defaults to 1.0 (sample everything);
    /// leave it there until volume justifies tail sampling at the collector.
    /// Overridden by <c>OTEL_TRACES_SAMPLER</c> / <c>OTEL_TRACES_SAMPLER_ARG</c>.
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;
}
