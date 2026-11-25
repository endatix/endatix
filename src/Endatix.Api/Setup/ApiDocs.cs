using Endatix.Api.Builders;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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
    /// Adds the document, Scalar and the opt-in Swagger UI once per application. A second call changes nothing, so a
    /// host that adds the docs through two builders does not map the docs page twice.
    /// </summary>
    internal static void Add(IApplicationBuilder app, ApiOptions options)
    {
        if (!options.UseSwagger || app.Properties.ContainsKey(ADDED_KEY))
        {
            return;
        }

        app.Properties[ADDED_KEY] = true;
        var documentPath = AddDocument(app, options);
        if (options.UseSwaggerUi)
        {
            AddSwaggerUi(app, options);
        }

        MapScalar(app, PagePath(options.SwaggerPath), documentPath);
    }

    /// <summary>Adds the document middleware and returns its path, read after the host's settings apply.</summary>
    private static Func<string> AddDocument(IApplicationBuilder app, ApiOptions options)
    {
        var documentPath = new NSwag.AspNetCore.OpenApiDocumentMiddlewareSettings().Path;
        app.UseOpenApi(settings =>
        {
            options.ConfigureOpenApiDocument?.Invoke(settings);
            documentPath = settings.Path;
        });
        return () => documentPath;
    }

    /// <summary>
    /// Maps Scalar over the document. Scalar takes the document path from the browser's address, so the page finds the
    /// document under a path base too.
    /// </summary>
    private static void MapScalar(IApplicationBuilder app, string pagePath, Func<string> documentPath) =>
        AsRouteBuilder(app).MapScalarApiReference(pagePath, scalar => scalar
            .WithTitle("API Reference")
            .WithTheme(ScalarTheme.Default)
            .WithOpenApiRoutePattern(documentPath())
            .AddDocument(DOCUMENT_NAME)
            .AddPreferredSecuritySchemes(BEARER_SCHEME));

    private static void AddSwaggerUi(IApplicationBuilder app, ApiOptions options) =>
        app.UseSwaggerUi(settings => settings.ConfigureDefaults(ui =>
        {
            ui.Path = SWAGGER_UI_PATH;
            options.ConfigureSwaggerUi?.Invoke(ui);
        }));

    /// <summary>
    /// The docs page path without a trailing slash. An empty path or <c>/</c> falls back to the default, since Scalar
    /// maps <c>{path}/{documentName?}</c> and a root page would answer every one-segment path.
    /// </summary>
    internal static string PagePath(string? swaggerPath)
    {
        var path = swaggerPath?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(path) ? DEFAULT_PATH : path;
    }

    private static IEndpointRouteBuilder AsRouteBuilder(IApplicationBuilder app) =>
        app as IEndpointRouteBuilder
        ?? throw new InvalidOperationException(
            "The API docs page needs endpoint routing. Add the Endatix API to a WebApplication.");
}
