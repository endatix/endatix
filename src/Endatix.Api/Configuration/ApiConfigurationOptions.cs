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
    /// Gets or sets the path of the API docs page, which shows Scalar. Must start with "/". Null means "/api-docs", or
    /// <see cref="SwaggerPath"/> when only that is set.
    /// </summary>
    [RegularExpression("^/.*", ErrorMessage = "API docs path must start with '/'")]
    public string? ApiDocsPath { get; set; }

    /// <summary>
    /// Gets or sets the deprecated name of <see cref="ApiDocsPath"/>. It applies only when
    /// <see cref="ApiDocsPath"/> is null. Null means "/api-docs".
    /// </summary>
    [RegularExpression("^/.*", ErrorMessage = "Swagger path must start with '/'")]
#pragma warning disable S1133 // Kept obsolete for callers-only / tombstone policy (Loop A #1472); removal is a separate decision.
    [Obsolete("Use ApiDocsPath")]
    public string? SwaggerPath { get; set; }
#pragma warning restore S1133

    /// <summary>
    /// Gets or sets a value indicating whether to also serve Swagger UI, at "/swagger". Off by default.
    /// </summary>
    public bool UseSwaggerUi { get; set; }
} 