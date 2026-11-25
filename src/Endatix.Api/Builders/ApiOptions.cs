using FastEndpoints;
using NSwag.AspNetCore;

namespace Endatix.Api.Builders;

/// <summary>
/// Core options for configuring Endatix API features.
/// </summary>
/// <remarks>
/// This class is the definitive source of API features configuration used throughout the application.
/// Only a subset of these properties can be configured via appsettings.json through the
/// <see cref="Configuration.ApiConfigurationOptions"/> class.
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
    /// Gets or sets a value indicating whether to serve the API docs: the OpenAPI document, the Scalar docs page, and
    /// Swagger UI when <see cref="UseSwaggerUi"/> is on. When <c>false</c>, none of them is served.
    /// This can be configured via appsettings.json.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the API docs page, which shows Scalar. Must start with "/".
    /// Default is "/api-docs"; an empty path or "/" also means "/api-docs". This can be configured via appsettings.json.
    /// </summary>
    public string? SwaggerPath { get; set; } = "/api-docs";

    /// <summary>
    /// Gets or sets a value indicating whether to also serve Swagger UI, at "/swagger". Off by default.
    /// It needs <see cref="UseSwagger"/>. This can be configured via appsettings.json.
    /// </summary>
    public bool UseSwaggerUi { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure OpenAPI document generation settings.
    /// more info: https://fast-endpoints.com/docs/swagger-support#describe-endpoints
    /// </summary>
    public Action<OpenApiDocumentMiddlewareSettings>? ConfigureOpenApiDocument { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure Swagger UI display settings. It applies when <see cref="UseSwaggerUi"/> is on.
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