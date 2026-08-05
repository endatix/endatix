using System.Reflection;
using Endatix.Hosting.Builders.Logging;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Instrumentation sources Endatix can register. Used with
/// <see cref="EndatixTelemetryBuilder.DisableInstrumentation"/>.
/// </summary>
/// <remarks>
/// Process instrumentation is deliberately absent: <c>OpenTelemetry.Instrumentation.Process</c>
/// has no stable release, and Endatix.Hosting is a published package. In a cluster, cAdvisor and
/// node-exporter already report process CPU and memory.
/// </remarks>
[Flags]
public enum Instrumentations
{
    /// <summary>No instrumentation.</summary>
    None = 0,

    /// <summary>Inbound HTTP requests (ASP.NET Core).</summary>
    AspNetCore = 1,

    /// <summary>Outbound HTTP calls (HttpClient) — includes webhook delivery.</summary>
    HttpClient = 2,

    /// <summary>.NET runtime metrics: GC, thread pool, allocation.</summary>
    Runtime = 4,

    /// <summary>Every source above.</summary>
    All = AspNetCore | HttpClient | Runtime
}

/// <summary>
/// Builder for configuring OpenTelemetry metrics and traces in the Endatix application.
/// </summary>
/// <remarks>
/// <para>
/// Telemetry is <em>off by default</em>: with no OTLP endpoint configured nothing is exported and
/// no exporter is allocated. Setting <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is all it takes to turn it on.
/// </para>
/// <para>
/// Standard <c>OTEL_*</c> environment variables are authoritative; anything under
/// <c>Endatix:Telemetry</c> is a fallback, never an override.
/// </para>
/// <para>
/// Registration is deferred to <see cref="Build"/> (invoked by
/// <c>EndatixBuilder.FinalizeConfiguration()</c>) so that calls such as
/// <see cref="DisableInstrumentation"/> take effect regardless of the order they are made in.
/// </para>
/// </remarks>
/// <example>
/// endatix.Telemetry
///     .UseDefaults()
///     .DisableInstrumentation(Instrumentations.Runtime)
///     .Build();
/// </example>
public class EndatixTelemetryBuilder
{
    /// <summary>
    /// Environment variable names read directly, per the OpenTelemetry specification.
    /// </summary>
    internal static class EnvVars
    {
        public const string ServiceName = "OTEL_SERVICE_NAME";
        public const string ServiceVersion = "OTEL_SERVICE_VERSION";
        public const string OtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
        public const string OtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
        public const string TracesSampler = "OTEL_TRACES_SAMPLER";
    }

    private static readonly string[] _defaultExcludedPaths = ["/health", "/alive", "/ready"];

    private readonly EndatixBuilder _parent;
    private readonly ILogger _logger;
    private readonly List<string> _excludedPaths = [.. _defaultExcludedPaths];

    private TelemetryOptions _options = new();
    private Instrumentations _instrumentation = Instrumentations.All;
    private bool _enabled;
    private bool _applied;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndatixTelemetryBuilder"/> class.
    /// </summary>
    /// <param name="parent">The parent builder.</param>
    public EndatixTelemetryBuilder(EndatixBuilder parent)
    {
        _parent = parent;
        _logger = parent.LoggerFactory.CreateLogger<EndatixTelemetryBuilder>();
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => _parent.Services;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration => _parent.Configuration;

    /// <summary>
    /// Enables telemetry with recommended defaults: all instrumentation sources, and the OTLP
    /// exporter when an endpoint is configured. Registration happens at <see cref="Build"/>.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixTelemetryBuilder UseDefaults()
    {
        _options = BindOptions();
        _enabled = true;
        return this;
    }

    /// <summary>
    /// Enables the OTLP exporter explicitly, optionally overriding the endpoint. Implicit whenever
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> or <c>Endatix:Telemetry:Otlp:Endpoint</c> is set.
    /// </summary>
    /// <param name="endpoint">Optional collector endpoint, for example <c>http://collector:4317</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixTelemetryBuilder WithOtlpExporter(string? endpoint = null)
    {
        if (!_enabled)
        {
            _options = BindOptions();
            _enabled = true;
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _options.Otlp.Endpoint = endpoint;
        }

        return this;
    }

    /// <summary>
    /// Turns off one or more instrumentation sources.
    /// </summary>
    /// <param name="instrumentation">The sources to disable; combinable with <c>|</c>.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixTelemetryBuilder DisableInstrumentation(Instrumentations instrumentation)
    {
        _instrumentation &= ~instrumentation;
        return this;
    }

    /// <summary>
    /// Adds request paths that must not produce traces. <c>/health</c>, <c>/alive</c> and
    /// <c>/ready</c> are excluded already.
    /// </summary>
    /// <param name="paths">Path prefixes to exclude, matched case-insensitively.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixTelemetryBuilder ExcludeFromTracing(params string[] paths)
    {
        _excludedPaths.AddRange(paths.Where(p => !string.IsNullOrWhiteSpace(p)));
        return this;
    }

    /// <summary>
    /// Applies the accumulated telemetry configuration and returns to the parent builder.
    /// Safe to call more than once; only the first call registers.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixBuilder Build()
    {
        if (_applied || !_enabled)
        {
            return _parent;
        }

        _applied = true;

        var otlpEndpoint = ResolveOtlpEndpoint();
        if (otlpEndpoint is null)
        {
            // AC6: nothing configured means nothing registered — no exporter allocated, no SDK cost.
            _logger.LogTelemetrySkippedNoExporter();
            return _parent;
        }

        // Resolved here rather than inside the AddOtlpExporter callback: that callback is a named
        // options configuration action the SDK defers until the provider is built, so validating in
        // it would surface a bad protocol long after startup — or not at all. AC8 wants fail-fast.
        var otlpProtocol = ResolveOtlpProtocol();

        var resource = BuildResource();

        var otel = Services.AddOpenTelemetry().ConfigureResource(r => r.AddAttributes(resource));

        otel.WithMetrics(metrics =>
        {
            if (_instrumentation.HasFlag(Instrumentations.AspNetCore))
            {
                metrics.AddAspNetCoreInstrumentation();
            }

            if (_instrumentation.HasFlag(Instrumentations.HttpClient))
            {
                metrics.AddHttpClientInstrumentation();
            }

            if (_instrumentation.HasFlag(Instrumentations.Runtime))
            {
                metrics.AddRuntimeInstrumentation();
            }

            metrics.AddOtlpExporter((exporter, _) => ConfigureOtlp(exporter, otlpEndpoint, otlpProtocol));
        });

        otel.WithTracing(tracing =>
        {
            if (_instrumentation.HasFlag(Instrumentations.AspNetCore))
            {
                // AC5: health and liveness probes are noise — one span per probe per scrape interval.
                tracing.AddAspNetCoreInstrumentation(o =>
                    o.Filter = context => !IsExcludedPath(context.Request.Path));
            }

            if (_instrumentation.HasFlag(Instrumentations.HttpClient))
            {
                tracing.AddHttpClientInstrumentation();
            }

            ApplySampler(tracing);

            tracing.AddOtlpExporter(exporter => ConfigureOtlp(exporter, otlpEndpoint, otlpProtocol));
        });

        _logger.LogTelemetryConfigured(otlpEndpoint.ToString(), _instrumentation.ToString());

        return _parent;
    }

    private TelemetryOptions BindOptions()
    {
        var options = new TelemetryOptions();
        Configuration.GetSection(TelemetryOptions.SectionName).Bind(options);
        return options;
    }

    /// <summary>
    /// Resolves the OTLP endpoint env-first, or returns <see langword="null"/> when telemetry is
    /// not configured. Throws when a value is present but unusable (AC8) — silently not exporting
    /// is the failure mode this whole plan exists to remove.
    /// </summary>
    internal Uri? ResolveOtlpEndpoint()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVars.OtlpEndpoint);
        var source = EnvVars.OtlpEndpoint;

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = _options.Otlp.Endpoint;
            source = $"{TelemetryOptions.SectionName}:Otlp:Endpoint";
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"OpenTelemetry is misconfigured: '{source}' is set to '{raw}', which is not an " +
                "absolute http or https URI. Expected something like 'http://collector:4317'. " +
                "Fix the value or remove it to disable telemetry export.");
        }

        return uri;
    }

    private static void ConfigureOtlp(
        OtlpExporterOptions exporter,
        Uri endpoint,
        OtlpExportProtocol? protocol)
    {
        exporter.Endpoint = endpoint;

        if (protocol is not null)
        {
            exporter.Protocol = protocol.Value;
        }
    }

    /// <summary>
    /// Resolves the OTLP wire protocol env-first, or <see langword="null"/> to leave the SDK
    /// default (grpc). Throws on an unsupported value rather than exporting over the wrong wire.
    /// </summary>
    internal OtlpExportProtocol? ResolveOtlpProtocol()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVars.OtlpProtocol);
        var source = EnvVars.OtlpProtocol;

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = _options.Otlp.Protocol;
            source = $"{TelemetryOptions.SectionName}:Otlp:Protocol";
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException(
                $"OpenTelemetry is misconfigured: '{source}' is set to '{raw}', which is not a " +
                "supported OTLP protocol. Use 'grpc' or 'http/protobuf'.")
        };
    }

    /// <summary>
    /// Builds the resource attributes env-first (AC2). <c>OTEL_SERVICE_NAME</c> wins over
    /// <c>Endatix:Telemetry:ServiceName</c>, which wins over the entry assembly name.
    /// </summary>
    internal Dictionary<string, object> BuildResource()
    {
        var serviceName =
            Environment.GetEnvironmentVariable(EnvVars.ServiceName)
            ?? _options.ServiceName
            ?? Assembly.GetEntryAssembly()?.GetName().Name
            ?? "endatix-api";

        var attributes = new Dictionary<string, object>
        {
            [ResourceSemanticConventions.ServiceName] = serviceName
        };

        var serviceVersion =
            Environment.GetEnvironmentVariable(EnvVars.ServiceVersion)
            ?? _options.ServiceVersion;

        if (!string.IsNullOrWhiteSpace(serviceVersion))
        {
            attributes[ResourceSemanticConventions.ServiceVersion] = serviceVersion;
        }

        // Configured attributes are the weakest source: OTEL_RESOURCE_ATTRIBUTES is applied by the
        // SDK's own environment detector and overrides anything set here.
        foreach (var (key, value) in _options.ResourceAttributes)
        {
            attributes[key] = value;
        }

        return attributes;
    }

    private void ApplySampler(TracerProviderBuilder tracing)
    {
        // OTEL_TRACES_SAMPLER is authoritative — leave the SDK to read it.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVars.TracesSampler)))
        {
            return;
        }

        var ratio = _options.Traces.SamplingRatio;
        if (ratio is < 0.0 or > 1.0)
        {
            throw new InvalidOperationException(
                $"OpenTelemetry is misconfigured: '{TelemetryOptions.SectionName}:Traces:SamplingRatio' " +
                $"is {ratio}, which is outside the valid range 0.0 to 1.0.");
        }

        // 1.0 is the SDK default; registering a sampler for it only adds a layer.
        if (ratio < 1.0)
        {
            tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)));
        }
    }

    private bool IsExcludedPath(PathString path) =>
        _excludedPaths.Any(excluded =>
            path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Resource attribute keys from the OpenTelemetry semantic conventions.
/// </summary>
internal static class ResourceSemanticConventions
{
    public const string ServiceName = "service.name";
    public const string ServiceVersion = "service.version";
}
