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
/// The API docs: the OpenAPI document FastEndpoints generates, Scalar at the docs page, and Swagger UI beside it while
/// it is on.
/// </summary>
internal static class ApiDocs
{
    /// <summary>The name of the OpenAPI document <see cref="ApiConfigurationBuilder.AddSwagger"/> registers.</summary>
    internal const string DOCUMENT_NAME = "alpha-version";

    /// <summary>The JWT bearer scheme FastEndpoints adds to the document.</summary>
    internal const string BEARER_SCHEME = "JWTBearerAuth";

    internal const string DEFAULT_PATH = "/api-docs";

    /// <summary>Where Scalar serves when Swagger UI keeps <see cref="DEFAULT_PATH"/>.</summary>
    internal const string SCALAR_FALLBACK_PATH = "/scalar";

    private const string ADDED_KEY = "Endatix.Api.ApiDocsAdded";

    private const string CONFLICTS_LOGGED_KEY = "Endatix.Api.ApiDocsConflictsLogged";

    /// <summary>
    /// Adds the document, Scalar and Swagger UI once per application. A later call changes nothing and logs a warning,
    /// so a host that adds the docs through two builders does not map the docs page twice. Settings set twice under a
    /// new and a legacy key are logged first, also when the docs are off.
    /// </summary>
    internal static void Add(IApplicationBuilder app, ApiOptions options)
    {
        LogConflictsOnce(app, options.Docs);
        if (!options.Docs.Enabled || WasAdded(app))
        {
            return;
        }

        var routes = AsRouteBuilder(app);
        app.Properties[ADDED_KEY] = true;
        AddPages(app, routes, options);
    }

    private static void AddPages(IApplicationBuilder app, IEndpointRouteBuilder routes, ApiOptions options)
    {
        var requested = new RequestedDocsPath(options.Docs.Path, options.Docs.PathSetting);
        var documentPath = AddDocument(app, options);
        var swaggerUiPath = ApiDocsSwaggerUi.Add(app, options);
        var docs = new ResolvedDocs(requested, ResolvePagePath(app, PagePath(requested.Value), swaggerUiPath), swaggerUiPath);
        MapScalar(routes, docs.DocsPage, documentPath);
        MapOldSwaggerUiBookmark(routes, docs);
        RedirectRootWhenPathFallsBack(app, routes, docs);
        LogServing(app, docs);
    }

    private static void LogServing(IApplicationBuilder app, ResolvedDocs docs)
    {
        var logger = Logger(app);
        if (logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation("Serving the API docs at {ApiDocsPage}", docs.DocsPage);
        }

        if (docs.SwaggerUiPage is { } swaggerUiPage && logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation("Serving Swagger UI at {SwaggerUiPage}", swaggerUiPage);
        }
    }

    /// <summary>
    /// Logs the conflicts of the first call only, even an empty list, so a later call with the configured settings does
    /// not warn about a setting an explicit first call replaced.
    /// </summary>
    private static void LogConflictsOnce(IApplicationBuilder app, ApiDocsOptions docs)
    {
        if (!app.Properties.TryAdd(CONFLICTS_LOGGED_KEY, true) || docs.Conflicts.Count == 0)
        {
            return;
        }

        foreach (var conflict in docs.Conflicts)
        {
            Logger(app)?.LogWarning(
                "{Setting} '{Value}' and the deprecated {LegacySetting} '{LegacyValue}' are both set. The API docs use the first.",
                conflict.Setting,
                conflict.Value,
                conflict.LegacySetting,
                conflict.LegacyValue);
        }
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
    /// When Swagger UI asks for the docs page's path, Swagger UI keeps it, since that URL served Swagger UI before, and
    /// Scalar moves to <see cref="DEFAULT_PATH"/>, or to <see cref="SCALAR_FALLBACK_PATH"/> when the contested path is
    /// <see cref="DEFAULT_PATH"/>. Swagger UI's middleware runs before endpoints, so it would hide Scalar there.
    /// </summary>
    private static string ResolvePagePath(IApplicationBuilder app, string pagePath, string? swaggerUiPath)
    {
        if (swaggerUiPath is null || !ApiDocsSwaggerUi.SamePath(swaggerUiPath, pagePath))
        {
            return pagePath;
        }

        var movedTo = ApiDocsSwaggerUi.SamePath(pagePath, DEFAULT_PATH) ? SCALAR_FALLBACK_PATH : DEFAULT_PATH;
        Logger(app)?.LogWarning(
            "Swagger UI and the API docs page both asked for {SwaggerUiPage}. Swagger UI keeps it, and the API docs page serves at {ApiDocsPage}. Set Endatix:Api:Docs:Path or Endatix:Api:Docs:SwaggerUi:Path to choose.",
            swaggerUiPath,
            movedTo);
        return movedTo;
    }

    /// <summary>
    /// An empty or <c>/</c> requested path asked for the docs at the root, which they cannot take, so they serve at the
    /// default. The root then redirects there and a warning names the setting that asked. The redirect takes the last
    /// route order, so a host's own <c>GET /</c> wins without an ambiguous match. An unset (null) or other path adds no
    /// root route.
    /// </summary>
    private static void RedirectRootWhenPathFallsBack(IApplicationBuilder app, IEndpointRouteBuilder routes, ResolvedDocs docs)
    {
        if (docs.Requested.Value is null || docs.Requested.Value.Trim().TrimEnd('/').Length > 0)
        {
            return;
        }

        LogRootFallback(app, docs);
        routes.MapGet("/", (HttpRequest request) => Results.Redirect($"{request.PathBase}{docs.DocsPage}/"))
            .WithOrder(int.MaxValue)
            .ExcludeFromDescription();
    }

    /// <summary>
    /// Sends the old Swagger UI address, <c>{page}/index.html</c>, to Swagger UI while it is on, since the bookmark meant
    /// Swagger UI, and to the docs page otherwise. The literal route outranks Scalar's <c>{page}/{documentName?}</c>,
    /// which would take <c>index.html</c> for a document name. Both targets keep the path base.
    /// </summary>
    private static void MapOldSwaggerUiBookmark(IEndpointRouteBuilder routes, ResolvedDocs docs)
    {
        var bookmark = $"{docs.DocsPage}/index.html";
        var endpoint = docs.SwaggerUiPage is { } swaggerUiPath
            ? routes.MapGet(bookmark, (HttpRequest request) => Results.Redirect($"{request.PathBase}{swaggerUiPath}/index.html"))
            : routes.MapGet(bookmark, () => Results.Redirect("./"));
        endpoint.ExcludeFromDescription();
    }

    /// <summary>
    /// The docs page path without a trailing slash. An empty path or <c>/</c> falls back to the default, since Scalar
    /// maps <c>{path}/{documentName?}</c> and a root page would answer every one-segment path.
    /// </summary>
    internal static string PagePath(string? requestedPath)
    {
        var path = requestedPath?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(path) ? DEFAULT_PATH : path;
    }

    private static void LogRootFallback(IApplicationBuilder app, ResolvedDocs docs)
    {
        if (docs.Requested.Setting == ApiDocsOptions.SWAGGER_PATH_SETTING)
        {
            Logger(app)?.LogWarning(
                "{Setting} '{Path}' cannot put the API docs at the root. They serve at {ApiDocsPage}, and the root redirects there unless the host maps its own root. Endatix:Api:SwaggerPath is deprecated: set Endatix:Api:Docs:Path instead.",
                docs.Requested.Setting,
                docs.Requested.Value,
                docs.DocsPage);
            return;
        }

        Logger(app)?.LogWarning(
            "{Setting} '{Path}' cannot put the API docs at the root. They serve at {ApiDocsPage}, and the root redirects there unless the host maps its own root.",
            docs.Requested.Setting,
            docs.Requested.Value,
            docs.DocsPage);
    }

    internal static ILogger? Logger(IApplicationBuilder app) =>
        app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(ApiDocs).FullName!);

    private static IEndpointRouteBuilder AsRouteBuilder(IApplicationBuilder app) =>
        app as IEndpointRouteBuilder
        ?? throw new InvalidOperationException(
            "The API docs page needs endpoint routing. Add the Endatix API to a WebApplication.");

    /// <summary>A requested docs page path and the setting it came from, so a warning names that setting.</summary>
    private sealed record RequestedDocsPath(string? Value, string Setting);

    /// <summary>Where the docs serve once the settings apply: the Scalar page, and Swagger UI's path while it is on.</summary>
    private sealed record ResolvedDocs(RequestedDocsPath Requested, string DocsPage, string? SwaggerUiPage);
}
