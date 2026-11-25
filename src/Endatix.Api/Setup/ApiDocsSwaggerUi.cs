using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;

namespace Endatix.Api.Setup;

/// <summary>Swagger UI on the docs page, for a host that asks for it instead of Scalar.</summary>
internal static class ApiDocsSwaggerUi
{
    /// <summary>
    /// Adds Swagger UI at the page path and returns the path it serves at, without a trailing slash, once the host's
    /// <see cref="ApiOptions.ConfigureSwaggerUi"/> ran, since it may move the path. NSwag runs the settings callback
    /// while the middleware is added, so the path is known on return.
    /// </summary>
    internal static string Add(IApplicationBuilder app, ApiOptions options, string pagePath)
    {
        var path = pagePath;
        app.UseSwaggerUi(ui => ui.ConfigureDefaults(swaggerUi =>
        {
            swaggerUi.Path = pagePath;
            options.ConfigureSwaggerUi?.Invoke(swaggerUi);
            path = swaggerUi.Path;
        }));
        return ApiDocs.PagePath(path);
    }
}
