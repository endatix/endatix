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
    /// Gets or sets the path where the liveness probe is exposed.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Path"/> on purpose. This endpoint runs only checks tagged
    /// <c>self</c> or <c>live</c> — never the database — so it answers "is this process still
    /// running?" rather than "can it serve traffic?". Pointing a Kubernetes livenessProbe at a
    /// path that includes the database means a database blip restarts every pod, which removes
    /// capacity at the moment the system is already degraded.
    /// </remarks>
    public string LivenessPath { get; set; } = "/alive";

    /// <summary>
    /// Gets or sets the path where the readiness probe is exposed.
    /// </summary>
    /// <remarks>
    /// Runs only checks tagged <c>ready</c> — the database checks Endatix registers by default.
    /// Distinct from <see cref="Path"/>, which runs every registered check: a consumer adding an
    /// untagged slow or flaky check would otherwise put it on the readiness path and start
    /// evicting pods from the Service endpoints for something unrelated to serving traffic.
    /// </remarks>
    public string ReadinessPath { get; set; } = "/ready";

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

    /// <summary>
    /// Gets or sets a value indicating whether to expose the liveness endpoint at <see cref="LivenessPath"/>.
    /// </summary>
    public bool EnableLivenessEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to expose the readiness endpoint at <see cref="ReadinessPath"/>.
    /// </summary>
    public bool EnableReadinessEndpoint { get; set; } = true;
} 