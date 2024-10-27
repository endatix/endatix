using Endatix.Framework.Settings;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Represents the settings for WebHooks in the application.
/// </summary>
public class WebHookSettings : IEndatixSettings
{
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

