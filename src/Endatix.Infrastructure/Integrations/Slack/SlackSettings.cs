using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Integrations.Slack;

/// <summary>
/// POCO Class for per environment settings for Slack.
/// </summary>
public class SlackSettings
{
    /// <summary>
    /// The token provided by Slack
    /// </summary>
    [Required]
    public string? Token { get; set; }


    /// <summary>
    /// The Base Url for the Endatix Hub, e.g. https://admin.endatix.com
    /// </summary>
    [Required]
    public string? EndatixHubBaseUrl { get; set; }


    /// <summary>
    /// The id of the channel to which notifications should be posted
    /// </summary>
    [Required]
    public string? ChannelId { get; set; }

    /// <summary>
    /// A toggle for activating the integration
    /// </summary>
    [Required]
    public bool Active { get; set; }
}