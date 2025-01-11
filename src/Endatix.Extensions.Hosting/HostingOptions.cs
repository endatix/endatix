namespace Endatix.Extensions.Hosting;

/// <summary>
/// Configuration options for hosting-related settings
/// </summary>
public class HostingOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SECTION_NAME = "Hosting";

    /// <summary>
    /// Indicates whether the application is running in Azure environment.
    /// Default value: false
    /// </summary>
    public bool IsAzure { get; set; } = false;
}
