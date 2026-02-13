namespace Endatix.Core.Configuration;

/// <summary>
/// Root-level configuration for the Endatix Hub application.
/// </summary>
public sealed class HubSettings
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SectionName = "Endatix:Hub";

    /// <summary>
    /// The base URL for the Endatix Hub application (e.g. https://app.endatix.com).
    /// Used to create links and URLs to the Hub.
    /// </summary>
    public string HubBaseUrl { get; set; } = string.Empty;
}
