using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using NSwag.AspNetCore;

namespace Endatix.Api.Setup;

/// <summary>Swagger UI on the docs page, for a host that asks for it instead of Scalar.</summary>
internal static class ApiDocsSwaggerUi
{
    /// <summary>
    /// The path Swagger UI serves at once the host's <see cref="ApiOptions.ConfigureSwaggerUi"/> ran, since it may move
    /// the path: normalized, and <see cref="ApiDocs.ROOT_PATH"/> for the root. The settings are applied to a scratch
    /// copy, so the path is known before anything is registered.
    /// </summary>
    internal static string ServedPath(ApiOptions options, string pagePath)
    {
        var scratch = new SwaggerUiSettings();
        Configure(scratch, options, pagePath);
        return ApiDocs.NormalizePath(scratch.Path) ?? ApiDocs.ROOT_PATH;
    }

    /// <summary>Adds Swagger UI with the host's settings, at the path <see cref="ServedPath"/> resolved.</summary>
    internal static void Add(IApplicationBuilder app, ApiOptions options, string servedPath) =>
        app.UseSwaggerUi(ui =>
        {
            Configure(ui, options, servedPath);
            ui.Path = servedPath == ApiDocs.ROOT_PATH ? string.Empty : servedPath;
        });

    private static void Configure(SwaggerUiSettings ui, ApiOptions options, string pagePath) =>
        ui.ConfigureDefaults(swaggerUi =>
        {
            swaggerUi.Path = pagePath;
            options.ConfigureSwaggerUi?.Invoke(swaggerUi);
        });
}
