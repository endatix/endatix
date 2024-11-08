namespace Endatix.Core.Features.WebHooks;

/// <summary>
/// Defines the interface for a service that manages WebHook messages.
/// </summary>
public interface IWebHookService
{
    /// <summary>
    /// Enqueues a WebHook message for processing.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload carried by the message.</typeparam>
    /// <param name="message">The WebHook message to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnqueueWebHookAsync<TPayload>(WebHookMessage<TPayload> message, CancellationToken cancellationToken) where TPayload : notnull;
}