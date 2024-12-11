namespace Endatix.Core.Integrations.Slack;

public interface SlackCLient {
    /// <summary>
    /// Posts a message to a Slack channel through the Endatix Bot Slack app
    /// </summary>
    /// <param name="token">The Slack API bearer token</param>
    /// <param name="channelId">The id of the channel to post the message to</param>
    /// <param name="message">The body of the message</param>
    /// <returns>Task</returns>
    Task PostMessageAsync(string token, string channelId, string message);
}