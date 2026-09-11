using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Endatix.Hosting.HealthChecks;

/// <summary>
/// Factory for creating health check options.
/// </summary>
internal static class HealthCheckOptionsFactory
{
    /// <summary>
    /// Creates default health check options.
    /// </summary>
    /// <param name="responseWriter">Optional custom response writer.</param>
    /// <returns>Health check options.</returns>
    public static HealthCheckOptions CreateDefaultOptions(Func<HttpContext, HealthReport, Task>? responseWriter = null)
    {
        var options = new HealthCheckOptions();
        if (responseWriter is { })
        {
            options.ResponseWriter = responseWriter;
        }

        return options;
    }

    /// <summary>
    /// Creates options for the liveness endpoint: only checks tagged <c>self</c> or <c>live</c>.
    /// </summary>
    /// <remarks>
    /// Both tags are accepted because the host may register either. Endatix's own defaults tag the
    /// process check <c>self</c>; Aspire ServiceDefaults, when present, contributes <c>live</c>
    /// instead and Endatix skips its own to avoid duplicating it. A predicate matching no checks
    /// yields an empty report, which reports Healthy — the correct answer for liveness, since
    /// reaching the endpoint at all proves the process is up.
    /// </remarks>
    /// <returns>Health check options filtered to liveness checks.</returns>
    public static HealthCheckOptions CreateLivenessOptions()
    {
        return new HealthCheckOptions { Predicate = IsLivenessCheck };
    }

    /// <summary>
    /// Selects the checks that answer "is this process alive?". Exposed so the same rule can be used
    /// to warn when nothing matches, rather than duplicating the tag names at the call site.
    /// </summary>
    public static bool IsLivenessCheck(HealthCheckRegistration registration) =>
        registration.Tags.Contains(HealthCheckTags.Self) || registration.Tags.Contains(HealthCheckTags.Live);

    /// <summary>
    /// Creates options for the readiness endpoint: only checks tagged <c>ready</c>.
    /// </summary>
    /// <returns>Health check options filtered to readiness checks.</returns>
    public static HealthCheckOptions CreateReadinessOptions()
    {
        return new HealthCheckOptions { Predicate = IsReadinessCheck };
    }

    /// <summary>
    /// Selects the checks that answer "can this instance serve traffic?" — the dependencies Endatix
    /// tags <c>ready</c>, not every registration.
    /// </summary>
    public static bool IsReadinessCheck(HealthCheckRegistration registration) =>
        registration.Tags.Contains(HealthCheckTags.Ready);

    /// <summary>
    /// Creates health check options for JSON output.
    /// </summary>
    /// <returns>Health check options configured for JSON output.</returns>
    public static HealthCheckOptions CreateJsonOptions()
    {
        return new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        data = e.Value.Data
                    }),
                    totalDuration = report.TotalDuration
                };

                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    result,
                    new JsonSerializerOptions { WriteIndented = true });
            }
        };
    }

    /// <summary>
    /// Creates health check options for web UI output.
    /// </summary>
    /// <returns>Health check options configured for HTML output.</returns>
    public static HealthCheckOptions CreateWebUIOptions()
    {
        return new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "text/html";

                var color = report.Status == HealthStatus.Healthy ? "green" :
                            report.Status == HealthStatus.Degraded ? "orange" : "red";

                var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Health Check Status</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; }}
        h1 {{ color: {color}; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        tr:nth-child(even) {{ background-color: #f9f9f9; }}
        .healthy {{ color: green; }}
        .degraded {{ color: orange; }}
        .unhealthy {{ color: red; }}
    </style>
</head>
<body>
    <h1>Health Status: {report.Status}</h1>
    <p>Total Duration: {report.TotalDuration.TotalMilliseconds}ms</p>
    <table>
        <tr>
            <th>Service</th>
            <th>Status</th>
            <th>Duration</th>
            <th>Description</th>
        </tr>";

                foreach (var entry in report.Entries)
                {
                    var status = entry.Value.Status.ToString().ToLower();
                    html += $@"
        <tr>
            <td>{entry.Key}</td>
            <td class='{status}'>{entry.Value.Status}</td>
            <td>{entry.Value.Duration.TotalMilliseconds}ms</td>
            <td>{entry.Value.Description}</td>
        </tr>";
                }

                html += @"
    </table>
</body>
</html>";

                var bytes = System.Text.Encoding.UTF8.GetBytes(html);
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
        };
    }
}
