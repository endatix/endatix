namespace Endatix.Core.Integrations.Slack;

public interface ISlackClient {
    /// <summary>
    /// Posts a message to a Slack channel through the Endatix Bot Slack app
    /// </summary>
    /// <param name="message">The body of the message</param>
    /// <returns>Task</returns>
    private Task PostMessageAsync(string message);
}