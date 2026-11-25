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
    /// Gets or sets the API docs settings: the document, the docs page, and the UI it shows. This can be configured via
    /// appsettings.json under <c>Endatix:Api:Docs</c>.
    /// </summary>
    public ApiDocsOptions Docs { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs. Deprecated: use <see cref="ApiDocsOptions.Enabled"/>
    /// on <see cref="Docs"/>, which this property reads and writes.
    /// </summary>
    public bool UseSwagger
    {
        get => Docs.Enabled;
        set => Docs.Enabled = value;
    }

    /// <summary>
    /// Gets or sets the path of the API docs page. Deprecated: use <see cref="ApiDocsOptions.Path"/> on
    /// <see cref="Docs"/>, which this property writes. It reads that path, or "/api-docs" while it is unset.
    /// </summary>
    public string? SwaggerPath
    {
        get => Docs.Path ?? ApiDocsOptions.DEFAULT_PATH;
        set => Docs.SetPath(value, ApiDocsOptions.SWAGGER_PATH_SETTING);
    }

    /// <summary>
    /// Gets or sets a delegate to configure OpenAPI document generation settings.
    /// more info: https://fast-endpoints.com/docs/swagger-support#describe-endpoints
    /// </summary>
    public Action<OpenApiDocumentMiddlewareSettings>? ConfigureOpenApiDocument { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure Swagger UI display settings, its <c>Path</c> included. Setting it shows Swagger
    /// UI on the docs page unless <see cref="ApiDocsOptions.Ui"/> is set.
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