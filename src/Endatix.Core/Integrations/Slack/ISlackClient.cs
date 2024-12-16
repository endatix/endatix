using Endatix.Core.Events;

namespace Endatix.Core.Integrations.Slack;

public interface ISlackClient {
    /// <summary>
    /// Posts a message to a Slack channel through the Endatix Bot Slack app
    /// </summary>
    /// <param name="notification">The notification object passed by the event publisher</param>
    /// /// <param name="cancellationToken">The event's cancellation token</param>
    /// <returns>Task</returns>
    Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken);
}