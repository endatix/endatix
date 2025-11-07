namespace Endatix.Core.Entities;

/// <summary>
/// Represents the complete webhook configuration for a tenant or form.
/// This configuration defines which webhook events are enabled and where they should be sent.
/// </summary>
public class WebHookConfiguration
{
    /// <summary>
    /// Dictionary of webhook events keyed by event name.
    /// Supported events: FormCreated, FormUpdated, FormEnabledStateChanged, FormDeleted, SubmissionCompleted
    /// </summary>
    public Dictionary<string, WebHookEventConfig> Events { get; set; } = new();
}

/// <summary>
/// Configuration for a specific webhook event.
/// </summary>
public class WebHookEventConfig
{
    /// <summary>
    /// Indicates whether this webhook event is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// List of endpoints where webhook payloads should be sent for this event.
    /// </summary>
    public List<WebHookEndpointConfig> WebHookEndpoints { get; set; } = new();
}

/// <summary>
/// Configuration for a webhook endpoint destination.
/// </summary>
public class WebHookEndpointConfig
{
    /// <summary>
    /// The URL where the webhook payload should be sent.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional authentication configuration for the webhook endpoint.
    /// </summary>
    public WebHookAuthConfig? Authentication { get; set; }
}

/// <summary>
/// Authentication configuration for webhook endpoints.
/// </summary>
public class WebHookAuthConfig
{
    /// <summary>
    /// The type of authentication. Supported values: "None", "ApiKey"
    /// </summary>
    public string Type { get; set; } = "None";

    /// <summary>
    /// The API key value (required when Type is "ApiKey").
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The header name for the API key (required when Type is "ApiKey").
    /// </summary>
    public string? ApiKeyHeader { get; set; }
}
