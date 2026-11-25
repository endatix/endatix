using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
    /// Gets or sets the API docs settings: the document, the docs page, and the UI it shows.
    /// </summary>
    [Experimental("ENDATIX0006")]
    public DocsSection Docs { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to serve the API docs. Deprecated: use <c>Docs:Enabled</c>, which wins
    /// when both are set.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the API docs page. Deprecated: use <c>Docs:Path</c>, which wins when both are set.
    /// Defaults to "/api-docs"; it applies only when the key is in the configuration.
    /// </summary>
    [RegularExpression("^/.*", ErrorMessage = "Swagger path must start with '/'")]
    public string? SwaggerPath { get; set; } = "/api-docs";

    /// <summary>The <c>Endatix:Api:Docs</c> section.</summary>
    [Experimental("ENDATIX0006")]
    public sealed class DocsSection
    {
        /// <summary>Gets or sets a value indicating whether to serve the API docs. Null means unset (on).</summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the path of the API docs page. Null means "/api-docs". A missing leading slash is added.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the UI the docs page shows: <c>Scalar</c>, <c>SwaggerUi</c> or <c>None</c>, in any case. Null
        /// means Scalar. An unknown value logs a warning and shows Scalar.
        /// </summary>
        public string? Ui { get; set; }
    }
}
