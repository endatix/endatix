namespace Endatix.Framework.Configuration;

/// <summary>
/// Options specific to the hosting environment.
/// </summary>
public class HostingOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "Hosting";

    /// <summary>
    /// Indicates whether the application is running in Azure environment.
    /// </summary>
    public bool IsAzure { get; set; } = false;

    /// <summary>
    /// Gets or sets the Application Insights connection string.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }

    /// <summary>
    /// Gets or sets whether Application Insights is enabled.
    /// </summary>
    public bool EnableApplicationInsights { get; set; } = false;
} 