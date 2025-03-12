using FastEndpoints;
using NSwag.AspNetCore;

namespace Endatix.Api.Builders;

/// <summary>
/// Core options for configuring Endatix API features.
/// </summary>
/// <remarks>
/// This class is the definitive source of API configuration options and should be
/// referenced by other projects rather than duplicating these properties.
/// </remarks>
public class ApiOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use exception handling middleware.
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;

    /// <summary>
    /// Gets or sets the exception handler path.
    /// </summary>
    public string ExceptionHandlerPath { get; set; } = "/error";

    /// <summary>
    /// Gets or sets a value indicating whether to use Swagger middleware.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable Swagger in production.
    /// </summary>
    public bool EnableSwaggerInProduction { get; set; } = false;

    /// <summary>
    /// Gets or sets the custom path for Swagger UI endpoint. Must start with "/".
    /// Default is "/api-docs".
    /// </summary>
    public string? SwaggerPath { get; set; } = "/api-docs";

    /// <summary>
    /// Gets or sets a delegate to configure OpenAPI document generation settings.
    /// more info: https://fast-endpoints.com/docs/swagger-support#describe-endpoints
    /// </summary>
    public Action<OpenApiDocumentMiddlewareSettings>? ConfigureOpenApiDocument { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure Swagger UI display settings.
    /// more info: https://fast-endpoints.com/docs/swagger-support#describe-endpoints
    /// </summary>
    public Action<SwaggerUiSettings>? ConfigureSwaggerUi { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use CORS middleware.
    /// </summary>
    public bool UseCors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use API versioning.
    /// </summary>
    public bool UseVersioning { get; set; } = true;

    /// <summary>
    /// Gets or sets the versioning prefix.
    /// </summary>
    public string VersioningPrefix { get; set; } = "v";

    /// <summary>
    /// Gets or sets the route prefix.
    /// </summary>
    public string RoutePrefix { get; set; } = "api";

    /// <summary>
    /// Gets or sets a delegate to configure FastEndpoints.
    /// </summary>
    public Action<Config>? ConfigureFastEndpoints { get; set; }
}