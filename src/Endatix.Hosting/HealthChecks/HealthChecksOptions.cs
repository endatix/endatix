using Microsoft.AspNetCore.Http;

namespace Endatix.Hosting.HealthChecks;

/// <summary>
/// Options for configuring health checks middleware.
/// </summary>
public class HealthChecksOptions
{
    /// <summary>
    /// Gets or sets the path where health checks will be exposed.
    /// </summary>
    /// <remarks>
    /// The default path is '/health'. The following additional endpoints will be created:
    /// - {Path}/detail - Returns detailed JSON output
    /// - {Path}/ui - Returns HTML UI for health checks
    /// </remarks>
    public string Path { get; set; } = "/health";

    /// <summary>
    /// Gets or sets a custom response writer for the health checks endpoint.
    /// </summary>
    public Func<HttpContext, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport, Task>? ResponseWriter { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable the JSON detail view at {Path}/detail.
    /// </summary>
    public bool EnableJsonView { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable the HTML UI view at {Path}/ui.
    /// </summary>
    public bool EnableWebUI { get; set; } = true;
} 