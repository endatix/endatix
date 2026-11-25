using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSwag.AspNetCore;
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
        MapOldSwaggerUiBookmark(routes, pagePath);
        RedirectRootWhenPathFallsBack(app, routes, options.SwaggerPath);
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

    /// <summary>
    /// Adds the document middleware and returns its path once the host's settings apply. NSwag reads a host's
    /// <c>Configure&lt;OpenApiDocumentMiddlewareSettings&gt;</c> only when no delegate is passed, so the delegate is
    /// passed only when the host set one.
    /// </summary>
    private static string AddDocument(IApplicationBuilder app, ApiOptions options)
    {
        if (options.ConfigureOpenApiDocument is null)
        {
            app.UseOpenApi();
            return RegisteredDocumentSettings(app).Path;
        }

        var documentPath = string.Empty;
        app.UseOpenApi(settings =>
        {
            options.ConfigureOpenApiDocument(settings);
            documentPath = settings.Path;
        });
        return documentPath;
    }

    private static OpenApiDocumentMiddlewareSettings RegisteredDocumentSettings(IApplicationBuilder app) =>
        app.ApplicationServices.GetService<IOptions<OpenApiDocumentMiddlewareSettings>>()?.Value
        ?? new OpenApiDocumentMiddlewareSettings();

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
    /// An empty or <c>/</c> SwaggerPath asked for the docs at the root, which they cannot take, so they serve at the
    /// default. The root then redirects there and a warning says so. An unset (null) or other path adds no root route,
    /// so a host keeps its root.
    /// </summary>
    private static void RedirectRootWhenPathFallsBack(
        IApplicationBuilder app, IEndpointRouteBuilder routes, string? swaggerPath)
    {
        if (swaggerPath is null || swaggerPath.Trim().TrimEnd('/').Length > 0)
        {
            return;
        }

        Logger(app)?.LogWarning(
            "SwaggerPath '{SwaggerPath}' cannot put the API docs at the root. They serve at {DocsPage}, and the root redirects there.",
            swaggerPath,
            DEFAULT_PATH);
        routes.MapGet("/", (HttpRequest request) => Results.Redirect($"{request.PathBase}{DEFAULT_PATH}/"))
            .ExcludeFromDescription();
    }

    /// <summary>
    /// Sends the old Swagger UI address, <c>{page}/index.html</c>, to the docs page. The literal route outranks Scalar's
    /// <c>{page}/{documentName?}</c>, which would take <c>index.html</c> for a document name. The relative target keeps
    /// the path base.
    /// </summary>
    private static void MapOldSwaggerUiBookmark(IEndpointRouteBuilder routes, string pagePath) =>
        routes.MapGet($"{pagePath}/index.html", () => Results.Redirect("./"))
            .ExcludeFromDescription();

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
                $"Swagger UI cannot serve at the API docs page '{pagePath}'. Set SwaggerPath to another path, such as " +
                $"{DEFAULT_PATH}, move Swagger UI with ConfigureSwaggerUi (set its Path), or turn UseSwaggerUi off.");
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
