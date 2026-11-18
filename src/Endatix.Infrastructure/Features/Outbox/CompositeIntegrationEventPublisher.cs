using Endatix.Core.Entities;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Fans a claimed outbox row out to every registered <see cref="IOutboxIntegrationEventHandler"/>
/// whose <see cref="IOutboxIntegrationEventHandler.EventTypes"/> includes the message event type.
/// </summary>
/// <remarks>
/// A handler's failure is wrapped with the handler's name, so the relay retries the message. An
/// <see cref="OutboxPermanentFailureException"/> passes through unwrapped, because the relay fails the message at
/// once only when it sees that exception itself.
/// </remarks>
internal sealed partial class CompositeIntegrationEventPublisher(
    IEnumerable<IOutboxIntegrationEventHandler> handlers,
    ILogger<CompositeIntegrationEventPublisher> logger,
    IEnumerable<AppLevelOutboxWork>? appLevelWorks = null) : IIntegrationEventPublisher
{
    private readonly IOutboxIntegrationEventHandler[] _handlers = handlers.ToArray();
    private readonly HashSet<Type> _appLevelWorks = [.. (appLevelWorks ?? []).Select(work => work.WorkType)];

    /// <inheritdoc />
    public async Task PublishAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        message = LegacyOutboxEvents.Translate(message);
        var matched = MatchedHandlers(message);
        if (matched.Length == 0)
        {
            LogNoHandler(message);
            return;
        }

        foreach (var handler in matched)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await handler.HandleAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OutboxPermanentFailureException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Outbox handler {handler.GetType().Name} failed for message {message.Id} ({message.EventType}).",
                    ex);
            }
        }
    }

    private IOutboxIntegrationEventHandler[] MatchedHandlers(IOutboxMessage message)
    {
        var matched = _handlers
            .Where(handler => handler.EventTypes.Contains(message.EventType, StringComparer.Ordinal))
            .ToArray();
        ThrowIfAppLevelWorkGetsATenantRow(matched, message);
        return matched;
    }

    // Checked before any handler runs, so the message fails whole, as it does when the relay enqueues jobs.
    private void ThrowIfAppLevelWorkGetsATenantRow(IOutboxIntegrationEventHandler[] matched, IOutboxMessage message)
    {
        if (message.TenantId == OutboxAppLevel.NoTenant)
        {
            return;
        }

        var appLevel = matched.FirstOrDefault(handler => _appLevelWorks.Contains(handler.GetType()));
        if (appLevel is not null)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} ({message.EventType}) names a tenant, but {appLevel.GetType().Name} " +
                $"is app-level work and runs on app-level messages only.");
        }
    }

    // Most lifecycle events have no built-in subscriber, so their rows are routine; an unknown type is worth a look.
    private void LogNoHandler(IOutboxMessage message)
    {
        if (SubmissionLifecycleEvent.EventTypes.All.Contains(message.EventType, StringComparer.Ordinal))
        {
            LogNoLifecycleHandler(message.Id, message.EventType);
            return;
        }

        LogNoHandler(message.Id, message.EventType);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox message {MessageId} has no handler for event type '{EventType}'.")]
    private partial void LogNoHandler(long messageId, string eventType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox message {MessageId} has no handler for lifecycle event type '{EventType}'.")]
    private partial void LogNoLifecycleHandler(long messageId, string eventType);
}
