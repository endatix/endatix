using System.ComponentModel.DataAnnotations;
using Endatix.Framework.Settings;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents the settings for WebHooks in the application.
/// </summary>
public class WebHookSettings : IEndatixSettings
{
    public const uint DEFAULT_PIPELINE_TIMEOUT_IN_SECONDS = 120;
    public const uint DEFAULT_ATTEMPT_TIMEOUT_IN_SECONDS = 10;
    public const int DEFAULT_RETRY_ATTEMPTS = 5;
    public const int DEFAULT_DELAY_IN_SECONDS = 10;
    public const int DEFAULT_MAX_CONCURRENT_REQUESTS = 5;
    public const int DEFAULT_MAX_QUEUE_SIZE = 25;

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



    /// <summary>
    /// Gets or sets the settings for the SubmissionCompleted event.
    /// </summary>
    public EventSetting SubmissionCompleted { get; set; } = new() { EventName = WebHooksPlugin.EventNames.FORM_SUBMITTED };

    /// <summary>
    /// Represents a setting for a specific event.
    /// </summary>
    public class EventSetting
    {
        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public required string EventName { get; init; }

        /// <summary>
        /// Gets or sets the URLs for the WebHooks associated with this event.
        /// </summary>
        public IEnumerable<string>? WebHookUrls { get; init; }
    }
}

