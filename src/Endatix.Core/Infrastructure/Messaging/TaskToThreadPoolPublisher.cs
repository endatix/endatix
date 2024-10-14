using MediatR;

namespace Endatix.Core.Infrastructure.Messaging;

/// <summary>
/// A "fire and forget" notification publisher that uses the ThreadPool for background execution.
/// This class is designed to publish notifications to their respective handlers in a non-blocking, asynchronous manner.
/// It leverages the ThreadPool to execute the handlers in the background, allowing the system to return control as soon as possible.
/// </summary>
public class TaskToThreadPoolPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            _ = Task.Run(() => handler.HandlerCallback(notification, cancellationToken), default);
        }

        return Task.CompletedTask;
    }
}