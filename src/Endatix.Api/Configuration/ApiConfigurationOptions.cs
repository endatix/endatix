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
    /// Gets or sets the API docs settings: the document, the Scalar docs page, and Swagger UI.
    /// </summary>
    public DocsSection Docs { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs. Deprecated: use <c>Docs:Enabled</c>, which wins
    /// when both are set. Null means unset.
    /// </summary>
    public bool? UseSwagger { get; set; }

    /// <summary>
    /// Gets or sets the path of the API docs page. Deprecated: use <c>Docs:Path</c>, which wins when both are set.
    /// </summary>
    [RegularExpression("^/.*", ErrorMessage = "Swagger path must start with '/'")]
    public string? SwaggerPath { get; set; }

    /// <summary>The <c>Endatix:Api:Docs</c> section.</summary>
    public sealed class DocsSection
    {
        /// <summary>Gets or sets a value indicating whether to serve the API docs. Null means unset (on).</summary>
        public bool? Enabled { get; set; }

        /// <summary>Gets or sets the path of the API docs page, which shows Scalar. Null means "/api-docs".</summary>
        [RegularExpression("^/.*", ErrorMessage = "API docs path must start with '/'")]
        public string? Path { get; set; }

        /// <summary>Gets or sets the Swagger UI settings.</summary>
        public SwaggerUiSection SwaggerUi { get; set; } = new();
    }

    /// <summary>The <c>Endatix:Api:Docs:SwaggerUi</c> section.</summary>
    public sealed class SwaggerUiSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to serve Swagger UI. Null means this release's default, which is on.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>Gets or sets the path of Swagger UI. Null means "/swagger".</summary>
        [RegularExpression("^/.*", ErrorMessage = "Swagger UI path must start with '/'")]
        public string? Path { get; set; }
    }
}
