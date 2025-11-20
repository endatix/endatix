namespace Endatix.Core.UseCases.TenantSettings;

/// <summary>
/// DTO for TenantSettings with sensitive data masked for security.
/// </summary>
public record TenantSettingsDto
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public long TenantId { get; init; }

    /// <summary>
    /// Submission token expiration time in hours. Null indicates tokens never expire.
    /// </summary>
    public int? SubmissionTokenExpiryHours { get; init; }

    /// <summary>
    /// Indicates whether submission tokens remain valid after submission completion.
    /// </summary>
    public bool IsSubmissionTokenValidAfterCompletion { get; init; }

    /// <summary>
    /// Slack integration settings with sensitive data masked.
    /// </summary>
    public SlackSettingsDto? SlackSettings { get; init; }

    /// <summary>
    /// Webhook configuration with sensitive data masked.
    /// </summary>
    public WebHookConfigurationDto? WebHookSettings { get; init; }

    /// <summary>
    /// Custom export configurations.
    /// </summary>
    public List<CustomExportConfigurationDto>? CustomExports { get; init; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// DTO for Slack settings with Token field masked.
/// </summary>
public record SlackSettingsDto
{
    /// <summary>
    /// The Slack token (masked for security).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// The Base URL for the Endatix Hub.
    /// </summary>
    public string? EndatixHubBaseUrl { get; init; }

    /// <summary>
    /// The Slack channel ID where notifications are posted.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <summary>
    /// Indicates whether Slack integration is active.
    /// </summary>
    public bool? Active { get; init; }
}

/// <summary>
/// DTO for webhook configuration with sensitive data masked.
/// </summary>
public record WebHookConfigurationDto
{
    /// <summary>
    /// Dictionary of webhook events keyed by event name, with sensitive data masked.
    /// </summary>
    public Dictionary<string, WebHookEventConfigDto> Events { get; init; } = new();
}

/// <summary>
/// DTO for webhook event configuration.
/// </summary>
public record WebHookEventConfigDto
{
    /// <summary>
    /// Indicates whether this webhook event is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// List of webhook endpoints with sensitive data masked.
    /// </summary>
    public List<WebHookEndpointConfigDto> WebHookEndpoints { get; init; } = new();
}

/// <summary>
/// DTO for webhook endpoint configuration.
/// </summary>
public record WebHookEndpointConfigDto
{
    /// <summary>
    /// The webhook URL.
    /// </summary>
    public string? Url { get; init; }
}

/// <summary>
/// DTO for custom export configuration.
/// </summary>
public record CustomExportConfigurationDto
{
    /// <summary>
    /// The ID of the custom export configuration.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The name of the custom export configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The name of the SQL function to call for the custom export.
    /// </summary>
    public required string SqlFunctionName { get; init; }
}
