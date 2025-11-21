namespace Endatix.Api.Endpoints.TenantSettings;

/// <summary>
/// API model for tenant settings with sensitive data masked for security.
/// </summary>
public class TenantSettingsModel
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Submission token expiration time in hours. Null indicates tokens never expire.
    /// </summary>
    public int? SubmissionTokenExpiryHours { get; set; }

    /// <summary>
    /// Indicates whether submission tokens remain valid after submission completion.
    /// </summary>
    public bool IsSubmissionTokenValidAfterCompletion { get; set; }

    /// <summary>
    /// Slack integration settings with sensitive data masked.
    /// </summary>
    public SlackSettingsModel? SlackSettings { get; set; }

    /// <summary>
    /// Webhook configuration with sensitive data masked.
    /// </summary>
    public WebHookConfigurationModel? WebHookSettings { get; set; }

    /// <summary>
    /// Custom export configurations.
    /// </summary>
    public List<CustomExportConfigurationModel>? CustomExports { get; set; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// API model for Slack settings with Token field masked.
/// </summary>
public class SlackSettingsModel
{
    /// <summary>
    /// The Slack token (masked for security).
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The Base URL for the Endatix Hub.
    /// </summary>
    public string? EndatixHubBaseUrl { get; set; }

    /// <summary>
    /// The Slack channel ID where notifications are posted.
    /// </summary>
    public string? ChannelId { get; set; }

    /// <summary>
    /// Indicates whether Slack integration is active.
    /// </summary>
    public bool? Active { get; set; }
}

/// <summary>
/// API model for webhook configuration with sensitive data masked.
/// </summary>
public class WebHookConfigurationModel
{
    /// <summary>
    /// Dictionary of webhook events keyed by event name, with sensitive data masked.
    /// </summary>
    public Dictionary<string, WebHookEventConfigModel> Events { get; set; } = new();
}

/// <summary>
/// API model for webhook event configuration.
/// </summary>
public class WebHookEventConfigModel
{
    /// <summary>
    /// Indicates whether this webhook event is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// List of webhook endpoints with sensitive data masked.
    /// </summary>
    public List<WebHookEndpointConfigModel> WebHookEndpoints { get; set; } = new();
}

/// <summary>
/// API model for webhook endpoint configuration.
/// </summary>
public class WebHookEndpointConfigModel
{
    /// <summary>
    /// The webhook URL.
    /// </summary>
    public string? Url { get; set; }
}

/// <summary>
/// API model for custom export configuration.
/// </summary>
public class CustomExportConfigurationModel
{
    /// <summary>
    /// The ID of the custom export configuration.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The name of the custom export configuration.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The name of the SQL function to call for the custom export.
    /// </summary>
    public required string SqlFunctionName { get; set; }
}
