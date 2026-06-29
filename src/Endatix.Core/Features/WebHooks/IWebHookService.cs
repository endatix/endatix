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

    /// <summary>
    /// Delivers a WebHook message <b>synchronously</b> — awaiting the HTTP POST to every matching endpoint —
    /// and reports whether delivery succeeded. Unlike <see cref="EnqueueWebHookAsync"/> (fire-and-forget via a
    /// background queue), this lets the caller react to failures. Used by the outbox relay so a row is marked
    /// <c>Sent</c> only after a real delivery, and retried otherwise (at-least-once).
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload carried by the message.</typeparam>
    /// <param name="tenantId">The tenant ID for which to deliver the webhook.</param>
    /// <param name="message">The WebHook message to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="formId">Optional form ID to use form-specific webhook configuration.</param>
    /// <returns><c>true</c> if there was nothing to deliver (no/disabled config or no endpoints) or every
    /// endpoint accepted the webhook; <c>false</c> if any endpoint failed.</returns>
    Task<bool> DeliverWebHookAsync<TPayload>(long tenantId, WebHookMessage<TPayload> message, CancellationToken cancellationToken, long? formId = null) where TPayload : notnull;
}