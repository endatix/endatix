namespace Endatix.Infrastructure.Features.WebHooks;
/// <summary>
/// Interface for managing a queue of long-running background tasks.
/// </summary>
public interface IBackgroundTasksQueue
{
    /// <summary>
    /// Adds a new background work item to the queue.
    /// </summary>
    /// <param name="workItem">The work item to be executed in the background.</param>
    ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> workItem);

    /// <summary>
    /// Retrieves the next available background work item from the queue.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A ValueTask that represents the dequeued work item.</returns>
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}