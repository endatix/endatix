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

    /// <summary>
    /// Gets or sets whether HSTS middleware should run.
    /// </summary>
    public bool? UseHsts { get; set; }

    /// <summary>
    /// Gets or sets whether HTTPS redirection middleware should run.
    /// </summary>
    public bool? UseHttpsRedirection { get; set; }

    /// <summary>
    /// Gets or sets reverse proxy hosting options.
    /// </summary>
    public ReverseProxyOptions ReverseProxy { get; set; } = new();
}

/// <summary>
/// Options for deployments where Endatix runs behind a reverse proxy.
/// </summary>
public class ReverseProxyOptions
{
    /// <summary>
    /// Gets or sets whether forwarded headers should be processed.
    /// Disabled by default so proxy headers are ignored unless the host opts in.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether all proxies should be trusted in Development.
    /// This is a local development convenience only; production keeps known proxy restrictions.
    /// </summary>
    public bool TrustAllProxiesInDevelopment { get; set; } = true;
}