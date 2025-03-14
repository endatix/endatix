using Endatix.Hosting.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

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
    /// Completes configuration and returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixMiddlewareBuilder Build() => _parent;
}