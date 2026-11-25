using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

namespace Endatix.Api.Setup;

/// <summary>
/// The API docs: the OpenAPI document FastEndpoints generates, Scalar at the docs page, and Swagger UI when a host
/// turns it on.
/// </summary>
internal static class ApiDocs
{
    /// <summary>The name of the OpenAPI document <see cref="ApiConfigurationBuilder.AddSwagger"/> registers.</summary>
    internal const string DOCUMENT_NAME = "alpha-version";

    /// <summary>The JWT bearer scheme FastEndpoints adds to the document.</summary>
    internal const string BEARER_SCHEME = "JWTBearerAuth";

    internal const string DEFAULT_PATH = "/api-docs";

    internal const string SWAGGER_UI_PATH = "/swagger";

    private const string ADDED_KEY = "Endatix.Api.ApiDocsAdded";

    /// <summary>
    /// Adds the document, Scalar and the opt-in Swagger UI once per application. A later call changes nothing and logs a
    /// warning, so a host that adds the docs through two builders does not map the docs page twice.
    /// </summary>
    internal static void Add(IApplicationBuilder app, ApiOptions options)
    {
        if (!options.UseSwagger || WasAdded(app))
        {
            return;
        }

        var routes = AsRouteBuilder(app);
        var pagePath = PagePath(options.SwaggerPath);
        app.Properties[ADDED_KEY] = true;
        var documentPath = AddDocument(app, options);
        AddSwaggerUi(app, options, pagePath);
        MapScalar(routes, pagePath, documentPath);
    }

    private static bool WasAdded(IApplicationBuilder app)
    {
        if (!app.Properties.ContainsKey(ADDED_KEY))
        {
            return false;
        }

        Logger(app)?.LogWarning("The API docs were already added. This call's settings are ignored.");
        return true;
    }

    /// <summary>Adds the document middleware and returns its path once the host's settings apply.</summary>
    private static string AddDocument(IApplicationBuilder app, ApiOptions options)
    {
        var documentPath = string.Empty;
        app.UseOpenApi(settings =>
        {
            options.ConfigureOpenApiDocument?.Invoke(settings);
            documentPath = settings.Path;
        });
        return documentPath;
    }

    /// <summary>
    /// Maps Scalar over the document. Scalar takes the document path from the browser's address, so the page finds the
    /// document under a path base too. Telemetry, Scalar's hosted fonts, its AI chat, its
    /// developer toolbar and its hosted client button are off, so the page calls no third party.
    /// </summary>
    private static void MapScalar(IEndpointRouteBuilder routes, string pagePath, string documentPath) =>
        routes.MapScalarApiReference(pagePath, scalar => scalar
            .WithTitle("API Reference")
            .WithTheme(ScalarTheme.Default)
            .DisableTelemetry()
            .DisableDefaultFonts()
            .DisableAgent()
            .DisableMcp()
            .HideDeveloperTools()
            .HideClientButton()
            .WithOpenApiRoutePattern(documentPath)
            .AddDocument(DOCUMENT_NAME)
            .AddPreferredSecuritySchemes(BEARER_SCHEME));

    /// <summary>
    /// Adds Swagger UI when it is on, at <c>/swagger</c> or where the host's settings move it. It must not take the docs
    /// page's path:
    /// its middleware runs before endpoints, so it would hide Scalar there.
    /// </summary>
    private static void AddSwaggerUi(IApplicationBuilder app, ApiOptions options, string pagePath)
    {
        if (!options.UseSwaggerUi)
        {
            if (options.ConfigureSwaggerUi is not null)
            {
                Logger(app)?.LogWarning("ConfigureSwaggerUi is set but UseSwaggerUi is off, so Swagger UI is not served.");
            }

            return;
        }

        app.UseSwaggerUi(settings => settings.ConfigureDefaults(ui =>
        {
            ui.Path = SWAGGER_UI_PATH;
            options.ConfigureSwaggerUi?.Invoke(ui);
            EnsureOffTheDocsPage(ui.Path, pagePath);
        }));
    }

    private static void EnsureOffTheDocsPage(string swaggerUiPath, string pagePath)
    {
        if (string.Equals(PagePath(swaggerUiPath), pagePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Swagger UI cannot serve at the API docs page '{pagePath}'. Move SwaggerPath or Swagger UI's path.");
        }
    }

    /// <summary>
    /// The docs page path without a trailing slash. An empty path or <c>/</c> falls back to the default, since Scalar
    /// maps <c>{path}/{documentName?}</c> and a root page would answer every one-segment path.
    /// </summary>
    internal static string PagePath(string? swaggerPath)
    {
        var path = swaggerPath?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(path) ? DEFAULT_PATH : path;
    }

    private static ILogger? Logger(IApplicationBuilder app) =>
        app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(ApiDocs).FullName!);

    private static IEndpointRouteBuilder AsRouteBuilder(IApplicationBuilder app) =>
        app as IEndpointRouteBuilder
        ?? throw new InvalidOperationException(
            "The API docs page needs endpoint routing. Add the Endatix API to a WebApplication.");
}
