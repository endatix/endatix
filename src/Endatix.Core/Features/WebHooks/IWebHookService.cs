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
    /// <param name="tenantId">The tenant ID for which to process the webhook.</param>
    /// <param name="message">The WebHook message to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="formId">Optional form ID to use form-specific webhook configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnqueueWebHookAsync<TPayload>(long tenantId, WebHookMessage<TPayload> message, CancellationToken cancellationToken, long? formId = null) where TPayload : notnull;
}