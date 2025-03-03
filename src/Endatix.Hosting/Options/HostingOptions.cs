using Microsoft.Extensions.Configuration;

namespace Endatix.Hosting.Options;

/// <summary>
/// Configuration options for hosting-related settings
/// </summary>
public class HostingOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SectionName = "Hosting";

    /// <summary>
    /// Indicates whether the application is running in Azure environment.
    /// Default value: false
    /// </summary>
    public bool IsAzure { get; set; } = false;

    /// <summary>
    /// Gets or sets the Application Insights connection string.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }

    /// <summary>
    /// Gets or sets whether Application Insights is enabled.
    /// </summary>
    public bool EnableApplicationInsights { get; set; }
} 