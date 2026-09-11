using Endatix.Hosting.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring health checks middleware.
/// </summary>
public class EndatixHealthChecksMiddlewareBuilder
{
    private readonly EndatixMiddlewareBuilder _parent;
    private readonly ILogger? _logger;
    private readonly HealthChecksOptions _options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EndatixHealthChecksMiddlewareBuilder"/> class.
    /// </summary>
    /// <param name="parent">The parent builder.</param>
    /// <param name="logger">The logger factory.</param>
    internal EndatixHealthChecksMiddlewareBuilder(EndatixMiddlewareBuilder parent, ILogger? logger = null)
    {
        _parent = parent;
        _logger = logger;
    }

    /// <summary>
    /// Configures health checks with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder UseDefaults()
    {
        // Default settings are already applied in the constructor
        return this;
    }

    /// <summary>
    /// Configures the path where health checks will be exposed.
    /// </summary>
    /// <param name="path">The path where health checks will be exposed.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithPath(string path)
    {
        _options.Path = path;
        return this;
    }

    /// <summary>
    /// Configures the path where the liveness probe is exposed. Defaults to "/alive".
    /// </summary>
    /// <param name="path">The path where the liveness probe will be exposed.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithLivenessPath(string path)
    {
        _options.LivenessPath = path;
        return this;
    }

    /// <summary>
    /// Configures the path where the readiness probe is exposed. Defaults to "/ready".
    /// </summary>
    /// <param name="path">The path where the readiness probe will be exposed.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithReadinessPath(string path)
    {
        _options.ReadinessPath = path;
        return this;
    }

    /// <summary>
    /// Stops the liveness endpoint from being mapped.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithoutLivenessEndpoint()
    {
        _options.EnableLivenessEndpoint = false;
        return this;
    }

    /// <summary>
    /// Stops the readiness endpoint from being mapped.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithoutReadinessEndpoint()
    {
        _options.EnableReadinessEndpoint = false;
        return this;
    }

    /// <summary>
    /// Stops the JSON detail view at {Path}/detail from being mapped.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithoutJsonView()
    {
        _options.EnableJsonView = false;
        return this;
    }

    /// <summary>
    /// Stops the HTML UI view at {Path}/ui from being mapped.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithoutWebUI()
    {
        _options.EnableWebUI = false;
        return this;
    }

    /// <summary>
    /// Configures a custom response writer for the health checks endpoint.
    /// </summary>
    /// <param name="responseWriter">The custom response writer.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksMiddlewareBuilder WithResponseWriter(Func<Microsoft.AspNetCore.Http.HttpContext, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport, Task> responseWriter)
    {
        _options.ResponseWriter = responseWriter;
        return this;
    }

    /// <summary>
    /// Applies every endpoint. Kept for callers that map health checks directly; <c>UseDefaults</c>
    /// uses <see cref="ApplyProbes"/> and <see cref="ApplyDiagnostics"/> so the two groups can sit
    /// on opposite sides of HTTPS redirection.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal void Apply(IApplicationBuilder app)
    {
        ApplyProbes(app);
        ApplyDiagnostics(app);
    }

    /// <summary>
    /// Maps the liveness and readiness probes. Register these BEFORE HSTS and HTTPS redirection:
    /// Kubernetes counts any 2xx-3xx as probe Success, so an HTTP probe answered with a 307 greens
    /// both probes permanently and readiness would never drop a pod whose database is gone.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal void ApplyProbes(IApplicationBuilder app)
    {
        ValidatePaths();

        if (_options.EnableLivenessEndpoint)
        {
            _logger?.LogInformation("Configuring liveness endpoint with path: {Path}", _options.LivenessPath);
            app.UseHealthChecks(_options.LivenessPath, HealthCheckOptionsFactory.CreateLivenessOptions());
            WarnIfNoLivenessChecksRegistered(app);
        }

        if (_options.EnableReadinessEndpoint)
        {
            _logger?.LogInformation("Configuring readiness endpoint with path: {Path}", _options.ReadinessPath);
            MapReadiness(app);
        }
    }

    /// <summary>
    /// Maps the unfiltered report and its detail/UI views. These stay BEHIND HTTPS redirection and
    /// HSTS — they expose check names, descriptions and durations, which must not be served in
    /// cleartext — and behind the API middleware so they inherit its CORS policy.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal void ApplyDiagnostics(IApplicationBuilder app)
    {
        _logger?.LogInformation("Configuring health checks middleware with path: {Path}", _options.Path);

        var healthCheckOptions = HealthCheckOptionsFactory.CreateDefaultOptions(_options.ResponseWriter);
        app.UseHealthChecks(_options.Path, healthCheckOptions);

        if (_options.EnableJsonView)
        {
            app.UseHealthChecks($"{_options.Path}/detail", HealthCheckOptionsFactory.CreateJsonOptions());
        }

        if (_options.EnableWebUI)
        {
            app.UseHealthChecks($"{_options.Path}/ui", HealthCheckOptionsFactory.CreateWebUIOptions());
        }
    }

    /// <summary>
    /// Readiness must fail closed. An empty predicate reports Healthy, so a readiness endpoint with
    /// no matching registration would answer 200 forever — Kubernetes would keep every pod in the
    /// Service endpoints while the database is unreachable and every request 500s. That state is
    /// reachable: the database checks are registered only when a DbContext is already in Services,
    /// so a host calling HealthChecks.UseDefaults() before or without persistence has none.
    /// </summary>
    private void MapReadiness(IApplicationBuilder app)
    {
        if (HasRegistrationMatching(app, HealthCheckOptionsFactory.IsReadinessCheck))
        {
            app.UseHealthChecks(_options.ReadinessPath, HealthCheckOptionsFactory.CreateReadinessOptions());
            return;
        }

        _logger?.LogError(
            "Readiness endpoint {Path} has no health check tagged '{Tag}', so it cannot tell whether " +
            "this instance can serve traffic. It will report Unhealthy rather than pass by default. " +
            "Register a '{TagAgain}'-tagged check (persistence registers one) or call " +
            "WithoutReadinessEndpoint() if this host is deliberately dependency-free.",
            _options.ReadinessPath,
            HealthCheckTags.Ready,
            HealthCheckTags.Ready);

        app.Map(_options.ReadinessPath, branch => branch.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                $"Unhealthy: no health check is tagged '{HealthCheckTags.Ready}'.");
        }));
    }

    /// <summary>
    /// Rejects path configuration that fails in ways the runtime reports badly or not at all: an
    /// empty path matches EVERY request and replaces the application with the health report, a path
    /// without a leading slash throws from inside Map naming no option, and a duplicate path is
    /// silently ignored because the first registration wins — which would quietly give the liveness
    /// probe the unfiltered report, the exact bug this split exists to fix.
    /// </summary>
    /// <remarks>
    /// Checks the routes actually mapped, not the options as written. A disabled probe's path is
    /// never registered, so it cannot collide with anything and must not fail startup. The
    /// detail/UI views are derived from <see cref="HealthChecksOptions.Path"/> rather than
    /// configured, but they occupy routes all the same — and because probes are mapped first, a
    /// liveness path of "{Path}/detail" would shadow the JSON view rather than conflict visibly.
    /// </remarks>
    private void ValidatePaths()
    {
        var configured = new List<(string Option, string Value)>
        {
            ("Endatix:Hosting:HealthCheckPath", _options.Path)
        };

        if (_options.EnableLivenessEndpoint)
        {
            configured.Add(("Endatix:Hosting:LivenessPath", _options.LivenessPath));
        }

        if (_options.EnableReadinessEndpoint)
        {
            configured.Add(("Endatix:Hosting:ReadinessPath", _options.ReadinessPath));
        }

        foreach (var (option, value) in configured)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{option} is empty. An empty health check path matches every request and would " +
                    "replace the entire application with the health report.");
            }

            if (!value.StartsWith('/'))
            {
                throw new InvalidOperationException(
                    $"{option} is '{value}' but must start with '/'.");
            }
        }

        var mapped = new List<(string Option, string Value)>(configured);

        if (_options.EnableJsonView)
        {
            mapped.Add(($"{_options.Path}/detail (the JSON view)", $"{_options.Path}/detail"));
        }

        if (_options.EnableWebUI)
        {
            mapped.Add(($"{_options.Path}/ui (the HTML view)", $"{_options.Path}/ui"));
        }

        var duplicate = mapped
            .GroupBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Health check paths must be distinct, but {string.Join(" and ", duplicate.Select(entry => entry.Option))} " +
                $"are both '{duplicate.Key}'. The first registration wins, so the others would be dead code.");
        }
    }

    private static bool HasRegistrationMatching(IApplicationBuilder app, Func<HealthCheckRegistration, bool> predicate)
    {
        var registrations = app.ApplicationServices
            .GetService<IOptions<HealthCheckServiceOptions>>()?.Value.Registrations;

        return registrations is not null && registrations.Any(predicate);
    }

    /// <summary>
    /// An empty predicate result reports Healthy, so a liveness endpoint with no matching
    /// registration answers 200 forever while proving only that Kestrel is listening. Unlike
    /// readiness this is not failed closed — a liveness probe that fails closed restart-loops the
    /// container — but it must not pass silently either.
    /// </summary>
    private void WarnIfNoLivenessChecksRegistered(IApplicationBuilder app)
    {
        if (HasRegistrationMatching(app, HealthCheckOptionsFactory.IsLivenessCheck))
        {
            return;
        }

        _logger?.LogWarning(
            "Liveness endpoint {Path} is mapped but no health check is tagged '{Self}' or '{Live}'. " +
            "It will report Healthy unconditionally and cannot detect an unhealthy process.",
            _options.LivenessPath,
            HealthCheckTags.Self,
            HealthCheckTags.Live);
    }

    /// <summary>
    /// Completes configuration and returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixMiddlewareBuilder Build() => _parent;
}