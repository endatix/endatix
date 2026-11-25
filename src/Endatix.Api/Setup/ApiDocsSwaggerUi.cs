using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Setup;

/// <summary>
/// Swagger UI beside the Scalar docs page. It is on unless a host turns it off; from the next release an unset setting
/// means off, so this release logs that once when the setting is unset.
/// </summary>
internal static class ApiDocsSwaggerUi
{
    internal const string DEFAULT_PATH = "/swagger";

    /// <summary>
    /// Adds Swagger UI while it is on and returns the path it serves at, without a trailing slash, once the host's
    /// <see cref="ApiOptions.ConfigureSwaggerUi"/> ran; null when it is off. NSwag runs the settings callback while
    /// the middleware is added, so the path is known before the docs page is mapped.
    /// </summary>
    internal static string? Add(IApplicationBuilder app, ApiOptions options)
    {
        var settings = options.Docs.SwaggerUi;
        if (settings.Enabled == false)
        {
            WarnWhenConfiguredButOff(app, options);
            return null;
        }

        string? path = null;
        app.UseSwaggerUi(ui => ui.ConfigureDefaults(swaggerUi =>
        {
            swaggerUi.Path = settings.Path ?? DEFAULT_PATH;
            options.ConfigureSwaggerUi?.Invoke(swaggerUi);
            path = swaggerUi.Path;
        }));
        var servedAt = Normalize(path ?? settings.Path ?? DEFAULT_PATH);
        AnnounceDefaultWhenUnset(app, settings, servedAt);
        return servedAt;
    }

    /// <summary>Whether two paths name the same page: trimmed, without a trailing slash, and ignoring case.</summary>
    internal static bool SamePath(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.Trim().TrimEnd('/');

    private static void WarnWhenConfiguredButOff(IApplicationBuilder app, ApiOptions options)
    {
        if (options.ConfigureSwaggerUi is not null)
        {
            ApiDocs.Logger(app)?.LogWarning(
                "ConfigureSwaggerUi is set but Endatix:Api:Docs:SwaggerUi:Enabled is false, so Swagger UI is not served.");
        }
    }

    private static void AnnounceDefaultWhenUnset(IApplicationBuilder app, SwaggerUiDocsOptions settings, string servedAt)
    {
        var logger = ApiDocs.Logger(app);
        if (settings.Enabled is null && logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation(
                "Swagger UI serves at {SwaggerUiPage} because Endatix:Api:Docs:SwaggerUi:Enabled is not set. Swagger UI will be off by default from the next release: set Endatix:Api:Docs:SwaggerUi:Enabled to true to keep it, or to false to turn it off now.",
                servedAt);
        }
    }
}
