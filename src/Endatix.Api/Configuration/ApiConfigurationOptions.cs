using System.ComponentModel.DataAnnotations;
using Endatix.Framework.Configuration;

namespace Endatix.Api.Configuration;

/// <summary>
/// Configuration options for API features that can be set via appsettings.json.
/// </summary>
/// <remarks>
/// This class provides a focused subset of ApiOptions that can be configured externally.
/// It acts as a configuration model that maps to the full ApiOptions used throughout the application.
/// </remarks>
public class ApiConfigurationOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "Api";

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs: the OpenAPI document, the Scalar docs page, and
    /// Swagger UI when <see cref="UseSwaggerUi"/> is on.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the API docs page, which shows Scalar. Must start with "/".
    /// </summary>
    [RegularExpression("^/.*", ErrorMessage = "Swagger path must start with '/'")]
    public string? SwaggerPath { get; set; } = "/api-docs";

    /// <summary>
    /// Gets or sets a value indicating whether to also serve Swagger UI, at "/swagger". Off by default.
    /// </summary>
    public bool UseSwaggerUi { get; set; }
} 