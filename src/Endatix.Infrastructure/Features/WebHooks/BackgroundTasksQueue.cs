using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// <inheritdoc/>
/// Implements a queue for managing background tasks.
/// </summary>
public class BackgroundTasksQueue : IBackgroundTasksQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;
    private readonly SemaphoreSlim _signal;

    public BackgroundTasksQueue(int capacity = 10)
    {
        // Capacity should be set based on the expected application load and number of concurrent threads accessing the queue. BoundedChannelFullMode.Wait will cause calls to WriteAsync() to return a task, which completes only when space became available. This leads to backpressure, in case too many publishers/calls start accumulating.
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);

        _signal = new SemaphoreSlim(0);
    }
    /// <summary>
    /// Adds a new background work item to the queue.
    /// </summary>
    /// <param name="workItem">The work item to be executed in the background.</param>
    public async ValueTask EnqueueAsync(Func<CancellationToken, ValueTask> workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

        await _queue.Writer.WriteAsync(workItem);
        _signal.Release();
    }

    /// <summary>
    /// Retrieves the next available background work item from the queue.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the dequeued work item.</returns>
    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        
        return workItem;
    }
}