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

    /// <summary>The served path of a page Swagger UI settings moved to the root.</summary>
    internal const string ROOT_PATH = "/";

    private const string DOCUMENT_PATH_KEY = "Endatix.Api.ApiDocsDocumentPath";

    private const string DOCUMENT_SETTINGS_KEY = "Endatix.Api.ApiDocsDocumentSettings";

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

        var page = ResolvePage(options, AddDocumentOnce(app, options));
        if (!MarkMapped(app, page.Path))
        {
            LogAlreadyMapped(app, page);
            return;
        }

        WarnAboutUi(app, options);
        AddPage(app, options, page);
    }

    private static DocsPage ResolvePage(ApiOptions options, string documentPath)
    {
        var requested = new RequestedDocsPath(options.Docs.Path, options.Docs.PathSetting);
        var ui = ResolveUi(options);
        return new DocsPage(requested, ServedPath(ui, options, requested.Value), documentPath, ui);
    }

    /// <summary>
    /// Adds the explicit docs call a host makes in code: it starts from the configured docs settings and Swagger UI
    /// settings with the docs on, then applies <paramref name="configure"/>. A conflict about a path the call replaced is
    /// dropped, so no warning names a setting the call overrode.
    /// </summary>
    internal static void AddExplicit(IApplicationBuilder app, Action<ApiOptions> configure)
    {
        var configuredOptions = ApiApplicationBuilderExtensions.GetApiOptions(app);
        var configured = configuredOptions.Docs;
        var options = new ApiOptions
        {
            Docs = configured.ForExplicitDocs(path: null),
            ConfigureSwaggerUi = configuredOptions.ConfigureSwaggerUi,
        };
        configure(options);
        if (!string.Equals(options.Docs.Path, configured.Path, StringComparison.Ordinal))
        {
            options.Docs.Conflicts = [];
        }

        Add(app, options);
    }

    private static void AddPage(IApplicationBuilder app, ApiOptions options, DocsPage page)
    {
        switch (page.Ui)
        {
            case ApiDocsUi.Scalar when app is IEndpointRouteBuilder routes:
                MapScalar(routes, page);
                break;
            case ApiDocsUi.Scalar:
                WarnNoEndpointRouting(app, page.Path);
                return;
            case ApiDocsUi.SwaggerUi:
                ApiDocsSwaggerUi.Add(app, options, page.Path);
                break;
            default:
                return;
        }

        RedirectRootWhenPathFallsBack(app, page.Requested, page.Path);
        LogServing(app, page.Path, page.Ui);
    }

    /// <summary>
    /// The path the page serves at: the normalized settings path, or for Swagger UI the path the host's Swagger UI
    /// settings leave it on, which is <c>/</c> when they move it to the root.
    /// </summary>
    private static string ServedPath(ApiDocsUi ui, ApiOptions options, string? requestedPath) =>
        ui == ApiDocsUi.SwaggerUi
            ? ApiDocsSwaggerUi.ServedPath(options, PagePath(requestedPath))
            : PagePath(requestedPath);

    /// <summary>
    /// The UI the settings ask for. Unset, a host that customized Swagger UI in code gets Swagger UI, and any other host
    /// gets Scalar. A value outside the enum shows Scalar too.
    /// </summary>
    private static ApiDocsUi ResolveUi(ApiOptions options) => options.Docs.Ui switch
    {
        null => options.ConfigureSwaggerUi is null ? ApiDocsUi.Scalar : ApiDocsUi.SwaggerUi,
        { } ui when Enum.IsDefined(ui) => ui,
        _ => ApiDocsUi.Scalar,
    };

    /// <summary>Warns when the set UI is not one, or leaves the host's Swagger UI settings unused.</summary>
    private static void WarnAboutUi(IApplicationBuilder app, ApiOptions options)
    {
        if (options.Docs.Ui is { } undefined && !Enum.IsDefined(undefined))
        {
            LogUnknownUi(app, ((int)undefined).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        if (options.Docs.Ui is { } ui && ui != ApiDocsUi.SwaggerUi && options.ConfigureSwaggerUi is not null)
        {
            Logger(app)?.LogWarning(
                "ConfigureSwaggerUi is set, but {Setting} is {Ui}, so the Swagger UI settings are unused.",
                ApiDocsOptions.UI_SETTING,
                ui);
        }
    }

    /// <summary>
    /// A repeated call for a mapped path is routine and logs at Debug. Swagger UI settings that move the page onto a
    /// path another page has would hide that page, so that page is skipped with a warning.
    /// </summary>
    private static void LogAlreadyMapped(IApplicationBuilder app, DocsPage page)
    {
        var logger = Logger(app);
        if (!string.Equals(page.Path, PagePath(page.Requested.Value), StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning(
                "The Swagger UI settings move the API docs page to {Page}, which already has a docs page. This page is skipped.",
                page.Path);
            return;
        }

        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug("The API docs page at {Page} is already mapped. This call changes nothing.", page.Path);
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

        return pages.Add(NormalizePath(path) ?? ROOT_PATH);
    }

    /// <summary>
    /// Adds the document middleware on the first call and returns its path, on every call. A later call's own
    /// <see cref="ApiOptions.ConfigureOpenApiDocument"/> cannot change the document, so a warning says it is ignored.
    /// </summary>
    private static string AddDocumentOnce(IApplicationBuilder app, ApiOptions options)
    {
        if (app.Properties.TryGetValue(DOCUMENT_PATH_KEY, out var stored) && stored is string added)
        {
            WarnWhenDocumentSettingsIgnored(app, options);
            return added;
        }

        var documentPath = AddDocument(app, options);
        app.Properties[DOCUMENT_PATH_KEY] = documentPath;
        app.Properties[DOCUMENT_SETTINGS_KEY] = options.ConfigureOpenApiDocument;
        return documentPath;
    }

    private static void WarnWhenDocumentSettingsIgnored(IApplicationBuilder app, ApiOptions options)
    {
        app.Properties.TryGetValue(DOCUMENT_SETTINGS_KEY, out var used);
        if (options.ConfigureOpenApiDocument is { } configure && !ReferenceEquals(configure, used))
        {
            Logger(app)?.LogWarning(
                "The OpenAPI document was already added, so this call's ConfigureOpenApiDocument settings are ignored.");
        }
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
    /// Maps Scalar over the document. Scalar takes the document path from the browser's address,
    /// so the page finds the document under a path base too. Telemetry, Scalar's hosted fonts, its AI chat, its
    /// developer toolbar and its hosted client button are off, so the page calls no third party.
    /// </summary>
    private static void MapScalar(IEndpointRouteBuilder routes, DocsPage page)
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
    /// root route, and neither does a page Swagger UI settings moved to the root.
    /// </summary>
    private static void RedirectRootWhenPathFallsBack(IApplicationBuilder app, RequestedDocsPath requested, string page)
    {
        if (requested.Value is null || NormalizePath(requested.Value) is not null || page == ROOT_PATH)
        {
            return;
        }

        if (app is not IEndpointRouteBuilder routes)
        {
            return;
        }

        LogRootFallback(app, requested, page);
        routes.MapGet("/", (HttpRequest request) => Results.Redirect($"{request.PathBase}{page}/"))
            .WithOrder(int.MaxValue)
            .ExcludeFromDescription();
    }

    /// <summary>
    /// The docs page path: trimmed, with a leading slash and without a trailing one. An empty path or <c>/</c> falls back
    /// to the default, since Scalar maps <c>{path}/{documentName?}</c> and a root page would answer every one-segment
    /// path.
    /// </summary>
    internal static string PagePath(string? requestedPath) => NormalizePath(requestedPath) ?? DEFAULT_PATH;

    /// <summary>A path trimmed, with one leading slash and no trailing one; null for an empty path or the root.</summary>
    internal static string? NormalizePath(string? path)
    {
        var trimmed = path?.Trim().Trim('/');
        return string.IsNullOrEmpty(trimmed) ? null : "/" + trimmed;
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

    /// <summary>Scalar is an endpoint, so without endpoint routing its page is skipped; startup never throws.</summary>
    private static void WarnNoEndpointRouting(IApplicationBuilder app, string page) =>
        Logger(app)?.LogWarning(
            "The API docs page at {Page} needs endpoint routing, so it is skipped. Add the Endatix API to a WebApplication.",
            page);

    /// <summary>A requested docs page path and the setting it came from, so a warning names that setting.</summary>
    private sealed record RequestedDocsPath(string? Value, string Setting);

    /// <summary>A docs page to map: what was asked, the path it serves at, the document it shows, and its UI.</summary>
    private sealed record DocsPage(RequestedDocsPath Requested, string Path, string DocumentPath, ApiDocsUi Ui);
}
