using Endatix.Hosting.HealthChecks;
using Microsoft.AspNetCore.Builder;
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
    /// Applies the configuration to the application builder.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal void Apply(IApplicationBuilder app)
    {
        _logger?.LogInformation("Configuring health checks middleware with path: {Path}", _options.Path);

        var healthCheckOptions = HealthCheckOptionsFactory.CreateDefaultOptions(_options.ResponseWriter);
        app.UseHealthChecks(_options.Path, healthCheckOptions);

        if (_options.EnableLivenessEndpoint)
        {
            _logger?.LogInformation("Configuring liveness endpoint with path: {Path}", _options.LivenessPath);
            app.UseHealthChecks(_options.LivenessPath, HealthCheckOptionsFactory.CreateLivenessOptions());
            WarnIfNoLivenessChecksRegistered(app);
        }

        if (_options.EnableReadinessEndpoint)
        {
            _logger?.LogInformation("Configuring readiness endpoint with path: {Path}", _options.ReadinessPath);
            app.UseHealthChecks(_options.ReadinessPath, HealthCheckOptionsFactory.CreateReadinessOptions());
        }

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
    /// An empty predicate result reports Healthy, so a liveness endpoint with no matching
    /// registration answers 200 forever while proving only that Kestrel is listening. That is a
    /// silent downgrade — it looks identical to a passing check — so say so at startup.
    /// </summary>
    private void WarnIfNoLivenessChecksRegistered(IApplicationBuilder app)
    {
        var registrations = app.ApplicationServices
            .GetService<IOptions<HealthCheckServiceOptions>>()?.Value.Registrations;

        if (registrations is null || registrations.Any(HealthCheckOptionsFactory.IsLivenessCheck))
        {
            return;
        }

        _logger?.LogWarning(
            "Liveness endpoint {Path} is mapped but no health check is tagged 'self' or 'live'. It " +
            "will report Healthy unconditionally and cannot detect an unhealthy process.",
            _options.LivenessPath);
    }

    /// <summary>
    /// Completes configuration and returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixMiddlewareBuilder Build() => _parent;
}