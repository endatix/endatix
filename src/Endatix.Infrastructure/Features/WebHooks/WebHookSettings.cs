using System.ComponentModel.DataAnnotations;
using Endatix.Framework.Settings;
using static Endatix.Infrastructure.Features.WebHooks.WebHooksPlugin;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents the settings for WebHooks in the application.
/// </summary>
public class WebHookSettings : IEndatixSettings
{

    /// <summary>
    /// Represents the settings for the HTTP server handling WebHooks.
    /// </summary>
    public HttpServerSettings ServerSettings { get; set; } = new();

    /// <summary>
    /// Represents the tenant-specific collection of WebHook events and their settings.
    /// Key is the tenant ID.
    /// </summary>
    public Dictionary<long, TenantWebHookConfig> Tenants { get; set; } = new();

    /// <summary>
    /// Represents the settings for the HTTP server handling WebHooks.
    /// </summary>
    public class HttpServerSettings
    {
        private const uint DEFAULT_PIPELINE_TIMEOUT_IN_SECONDS = 120;
        private const uint DEFAULT_ATTEMPT_TIMEOUT_IN_SECONDS = 10;
        private const int DEFAULT_RETRY_ATTEMPTS = 5;
        private const int DEFAULT_DELAY_IN_SECONDS = 10;
        private const int DEFAULT_MAX_CONCURRENT_REQUESTS = 5;
        private const int DEFAULT_MAX_QUEUE_SIZE = 25;

        /// <summary>
        /// Gets or sets the timeout for the webhook pipeline in seconds. Allowed range: 1-600.
        /// </summary>
        [Range(1, 600)]
        public uint PipelineTimeoutInSeconds { get; set; } = DEFAULT_PIPELINE_TIMEOUT_IN_SECONDS;

        /// <summary>
        /// Gets or sets the timeout for each attempt in seconds. Allowed range: 1-120.
        /// </summary>
        [Range(1, 120)]
        public uint AttemptTimeoutInSeconds { get; set; } = DEFAULT_ATTEMPT_TIMEOUT_IN_SECONDS;

        /// <summary>
        /// Gets or sets the number of retry attempts. Allowed range: 1-10.
        /// </summary>
        [Range(1, 10)]
        public int RetryAttempts { get; set; } = DEFAULT_RETRY_ATTEMPTS;

        /// <summary>
        /// Gets or sets the delay between retries in seconds. Allowed range: 1-60.
        /// </summary>
        [Range(1, 60)]
        public int Delay { get; set; } = DEFAULT_DELAY_IN_SECONDS;


        /// <summary>
        /// Gets or sets the maximum number of concurrent requests. Allowed range: 1-100.
        /// </summary>
        [Range(1, 100)]
        public int MaxConcurrentRequests { get; set; } = DEFAULT_MAX_CONCURRENT_REQUESTS;


        /// <summary>
        /// Gets or sets the maximum number of requests in the queue. Allowed range: 1-100.
        /// </summary>
        [Range(1, 100)]
        public int MaxQueueSize { get; set; } = DEFAULT_MAX_QUEUE_SIZE;
    }

    /// <summary>
    /// Represents a tenant's webhook configuration.
    /// </summary>
    public class TenantWebHookConfig
    {
        /// <summary>
        /// The webhook events configuration for this tenant.
        /// </summary>
        public WebHookEvents Events { get; set; } = new();
    }

    /// <summary>
    /// Represents the collection of WebHook events and their settings.
    /// </summary>
    public class WebHookEvents
    {
        /// <summary>
        /// Represents the settings for the 'FormCreated' event.
        /// </summary>
        public EventSetting FormCreated { get; set; } = new() { EventName = EventNames.FORM_CREATED };

        /// <summary>
        /// Represents the settings for the 'FormUpdated' event.
        /// </summary>
        public EventSetting FormUpdated { get; set; } = new() { EventName = EventNames.FORM_UPDATED };

        /// <summary>
        /// Represents the settings for the 'FormEnabledStateChanged' event.
        /// </summary>
        public EventSetting FormEnabledStateChanged { get; set; } = new() { EventName = EventNames.FORM_ENABLED_STATE_CHANGED };

        /// <summary>
        /// Represents the settings for the 'SubmissionCompleted' event.
        /// </summary>
        public EventSetting SubmissionCompleted { get; set; } = new() { EventName = EventNames.SUBMISSION_COMPLETED };

        /// <summary>
        /// Represents the settings for the 'FormDeleted' event.
        /// </summary>
        public EventSetting FormDeleted { get; set; } = new() { EventName = EventNames.FORM_DELETED };
    }

    /// <summary>
    /// Represents a setting for a specific event.
    /// </summary>
    public class EventSetting
    {
        /// <summary>
        /// Indicates whether the event is enabled.
        /// </summary>
        public bool IsEnabled { get; init; } = false;

        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public required string EventName { get; init; }

        /// <summary>
        /// Gets or sets the webhook endpoints for this event.
        /// </summary>
        public IEnumerable<WebHookEndpoint>? WebHookEndpoints { get; init; }

        /// <summary>
        /// Gets or sets the URLs for the WebHooks associated with this event.
        /// This property is maintained for backward compatibility.
        /// If WebHookEndpoints is null or empty, this will be used to create simple endpoints without authentication.
        /// </summary>
        public IEnumerable<string>? WebHookUrls { get; init; }

        /// <summary>
        /// Gets all webhook endpoints, combining both WebHookEndpoints and WebHookUrls for backward compatibility.
        /// </summary>
        public IEnumerable<WebHookEndpoint> GetAllEndpoints()
        {
            var endpoints = new List<WebHookEndpoint>();

            // Add configured endpoints
            if (WebHookEndpoints?.Any() == true)
            {
                endpoints.AddRange(WebHookEndpoints);
            }

            // Add simple URLs as endpoints without authentication (backward compatibility)
            if (WebHookUrls?.Any() == true)
            {
                endpoints.AddRange(WebHookUrls.Select(url => new WebHookEndpoint { Url = url }));
            }

            return endpoints;
        }
    }
}

/// <summary>
/// Represents a webhook endpoint with its authentication configuration.
/// </summary>
public class WebHookEndpoint
{
    /// <summary>
    /// Gets or sets the webhook URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets or sets the authentication configuration for this endpoint.
    /// </summary>
    public AuthenticationConfig? Authentication { get; init; }
}

/// <summary>
/// Represents the authentication configuration for a webhook endpoint.
/// </summary>
public class AuthenticationConfig
{
    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public AuthenticationType Type { get; init; } = AuthenticationType.None;

    /// <summary>
    /// Gets or sets the API key value. Used when Type is ApiKey.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets or sets the header name for the API key. Used when Type is ApiKey.
    /// </summary>
    public string? ApiKeyHeader { get; init; }
}

/// <summary>
/// Defines the available authentication types for webhook endpoints.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication.
    /// </summary>
    None,

    /// <summary>
    /// API key authentication using a custom header.
    /// </summary>
    ApiKey
}