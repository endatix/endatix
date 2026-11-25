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
/// The API docs: the OpenAPI document FastEndpoints generates, and a docs page that shows it with Scalar or Swagger UI.
/// </summary>
internal static class ApiDocs
{
    /// <summary>The name of the OpenAPI document <see cref="ApiConfigurationBuilder.AddSwagger"/> registers.</summary>
    internal const string DOCUMENT_NAME = "alpha-version";

    /// <summary>The JWT bearer scheme FastEndpoints adds to the document.</summary>
    internal const string BEARER_SCHEME = "JWTBearerAuth";

    internal const string DEFAULT_PATH = ApiDocsOptions.DEFAULT_PATH;

    private const string DOCUMENT_PATH_KEY = "Endatix.Api.ApiDocsDocumentPath";

    private const string PAGES_KEY = "Endatix.Api.ApiDocsPages";

    private const string SETTINGS_LOGGED_KEY = "Endatix.Api.ApiDocsSettingsLogged";

    /// <summary>
    /// Adds the document once per application and a docs page at the settings' path. A later call for another path
    /// adds a page there over the same document; a call for a path already mapped changes nothing. Settings set twice
    /// under a new and a legacy key are logged by the first call, also when the docs are off.
    /// </summary>
    internal static void Add(IApplicationBuilder app, ApiOptions options)
    {
        LogSettingsOnce(app, options.Docs);
        if (!options.Docs.Enabled)
        {
            return;
        }

        var documentPath = AddDocumentOnce(app, options);
        var requested = new RequestedDocsPath(options.Docs.Path, options.Docs.PathSetting);
        var page = new DocsPage(requested, PagePath(requested.Value), documentPath);
        if (!MarkMapped(app, page.Path))
        {
            LogAlreadyMapped(app, page.Path);
            return;
        }

        AddPage(app, options, page);
    }

    private static void AddPage(IApplicationBuilder app, ApiOptions options, DocsPage page)
    {
        var ui = EffectiveUi(app, options);
        var servedAt = ui switch
        {
            ApiDocsUi.Scalar => MapScalar(AsRouteBuilder(app), page),
            ApiDocsUi.SwaggerUi => ApiDocsSwaggerUi.Add(app, options, page.Path),
            _ => null,
        };
        if (servedAt is null)
        {
            return;
        }

        MarkMapped(app, servedAt);
        RedirectRootWhenPathFallsBack(app, page.Requested, servedAt);
        LogServing(app, servedAt, ui);
    }

    /// <summary>
    /// The UI the settings ask for. Unset, a host that customized Swagger UI in code gets Swagger UI, and any other host
    /// gets Scalar. A set UI wins, and a warning says when it leaves the host's Swagger UI settings unused.
    /// </summary>
    private static ApiDocsUi EffectiveUi(IApplicationBuilder app, ApiOptions options)
    {
        if (options.Docs.Ui is not { } ui)
        {
            return options.ConfigureSwaggerUi is null ? ApiDocsUi.Scalar : ApiDocsUi.SwaggerUi;
        }

        if (ui != ApiDocsUi.SwaggerUi && options.ConfigureSwaggerUi is not null)
        {
            Logger(app)?.LogWarning(
                "ConfigureSwaggerUi is set, but {Setting} is {Ui}, so the Swagger UI settings are unused.",
                ApiDocsOptions.UI_SETTING,
                ui);
        }

        return ui;
    }

    private static void LogAlreadyMapped(IApplicationBuilder app, string page)
    {
        var logger = Logger(app);
        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug("The API docs page at {Page} is already mapped. This call changes nothing.", page);
        }
    }

    private static void LogServing(IApplicationBuilder app, string page, ApiDocsUi ui)
    {
        var logger = Logger(app);
        if (logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation("Serving the API docs at {Page} with {Ui}", page, ui);
        }
    }

    /// <summary>
    /// Logs the setting warnings of the first call only, even when it has none, so a later call with the configured
    /// settings does not warn about a setting an explicit first call replaced.
    /// </summary>
    private static void LogSettingsOnce(IApplicationBuilder app, ApiDocsOptions docs)
    {
        if (!app.Properties.TryAdd(SETTINGS_LOGGED_KEY, true))
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

        LogUnknownUi(app, docs.UnknownUi);
    }

    private static void LogUnknownUi(IApplicationBuilder app, string? unknownUi)
    {
        if (unknownUi is not null)
        {
            Logger(app)?.LogWarning(
                "{Setting} '{Value}' is not a docs UI. Use one of {AcceptedValues}. The API docs show Scalar.",
                ApiDocsOptions.UI_SETTING,
                unknownUi,
                ApiDocsOptions.ACCEPTED_UI_VALUES);
        }
    }

    /// <summary>Records a page path, normalized, and returns whether it was not mapped before.</summary>
    private static bool MarkMapped(IApplicationBuilder app, string path)
    {
        if (!app.Properties.TryGetValue(PAGES_KEY, out var stored) || stored is not HashSet<string> pages)
        {
            pages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            app.Properties[PAGES_KEY] = pages;
        }

        return pages.Add(PagePath(path));
    }

    /// <summary>Adds the document middleware on the first call and returns its path, on every call.</summary>
    private static string AddDocumentOnce(IApplicationBuilder app, ApiOptions options)
    {
        if (app.Properties.TryGetValue(DOCUMENT_PATH_KEY, out var stored) && stored is string added)
        {
            return added;
        }

        var documentPath = AddDocument(app, options);
        app.Properties[DOCUMENT_PATH_KEY] = documentPath;
        return documentPath;
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
    /// Maps Scalar over the document and returns its path. Scalar takes the document path from the browser's address,
    /// so the page finds the document under a path base too. Telemetry, Scalar's hosted fonts, its AI chat, its
    /// developer toolbar and its hosted client button are off, so the page calls no third party.
    /// </summary>
    private static string MapScalar(IEndpointRouteBuilder routes, DocsPage page)
    {
        routes.MapScalarApiReference(page.Path, scalar => scalar
            .WithTitle("API Reference")
            .WithTheme(ScalarTheme.Default)
            .DisableTelemetry()
            .DisableDefaultFonts()
            .DisableAgent()
            .DisableMcp()
            .HideDeveloperTools()
            .HideClientButton()
            .WithOpenApiRoutePattern(page.DocumentPath)
            .AddDocument(DOCUMENT_NAME)
            .AddPreferredSecuritySchemes(BEARER_SCHEME));
        MapIndexRedirect(routes, page.Path);
        return page.Path;
    }

    /// <summary>
    /// Sends <c>{page}/index.html</c>, the old Swagger UI address, to the page. The literal route outranks Scalar's
    /// <c>{page}/{documentName?}</c>, which would take <c>index.html</c> for a document name. The relative target keeps
    /// the path base.
    /// </summary>
    private static void MapIndexRedirect(IEndpointRouteBuilder routes, string pagePath) =>
        routes.MapGet($"{pagePath}/index.html", () => Results.Redirect("./")).ExcludeFromDescription();

    /// <summary>
    /// An empty or <c>/</c> requested path asked for the docs at the root, which they cannot take, so they serve at the
    /// default. The root then redirects there and a warning names the setting that asked. The redirect takes the last
    /// route order, so a host's own <c>GET /</c> wins without an ambiguous match. An unset (null) or other path adds no
    /// root route.
    /// </summary>
    private static void RedirectRootWhenPathFallsBack(IApplicationBuilder app, RequestedDocsPath requested, string page)
    {
        if (requested.Value is null || requested.Value.Trim().TrimEnd('/').Length > 0)
        {
            return;
        }

        LogRootFallback(app, requested, page);
        AsRouteBuilder(app).MapGet("/", (HttpRequest request) => Results.Redirect($"{request.PathBase}{page}/"))
            .WithOrder(int.MaxValue)
            .ExcludeFromDescription();
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

    private static void LogRootFallback(IApplicationBuilder app, RequestedDocsPath requested, string page)
    {
        if (requested.Setting == ApiDocsOptions.SWAGGER_PATH_SETTING)
        {
            Logger(app)?.LogWarning(
                "{Setting} '{Path}' cannot put the API docs at the root. They serve at {ApiDocsPage}, and the root redirects there unless the host maps its own root. Endatix:Api:SwaggerPath is deprecated: set Endatix:Api:Docs:Path instead.",
                requested.Setting,
                requested.Value,
                page);
            return;
        }

        Logger(app)?.LogWarning(
            "{Setting} '{Path}' cannot put the API docs at the root. They serve at {ApiDocsPage}, and the root redirects there unless the host maps its own root.",
            requested.Setting,
            requested.Value,
            page);
    }

    internal static ILogger? Logger(IApplicationBuilder app) =>
        app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(ApiDocs).FullName!);

    private static IEndpointRouteBuilder AsRouteBuilder(IApplicationBuilder app) =>
        app as IEndpointRouteBuilder
        ?? throw new InvalidOperationException(
            "The API docs page needs endpoint routing. Add the Endatix API to a WebApplication.");

    /// <summary>A requested docs page path and the setting it came from, so a warning names that setting.</summary>
    private sealed record RequestedDocsPath(string? Value, string Setting);

    /// <summary>A docs page to map: what was asked, the page path it resolved to, and the document it shows.</summary>
    private sealed record DocsPage(RequestedDocsPath Requested, string Path, string DocumentPath);
}
