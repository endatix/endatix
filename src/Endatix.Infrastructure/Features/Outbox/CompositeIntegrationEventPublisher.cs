using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Fans a claimed outbox row out to every registered <see cref="IOutboxIntegrationEventHandler"/>
/// whose <see cref="IOutboxIntegrationEventHandler.EventTypes"/> includes the message event type.
/// </summary>
internal sealed partial class CompositeIntegrationEventPublisher(
    IEnumerable<IOutboxIntegrationEventHandler> handlers,
    ILogger<CompositeIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    private readonly IOutboxIntegrationEventHandler[] _handlers = handlers.ToArray();

    /// <inheritdoc />
    public async Task PublishAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        message = LegacyOutboxEvents.Translate(message);
        var matched = _handlers
            .Where(handler => handler.EventTypes.Contains(message.EventType, StringComparer.Ordinal))
            .ToArray();

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
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Outbox handler {handler.GetType().Name} failed for message {message.Id} ({message.EventType}).",
                    ex);
            }
        }
    }

    private void LogNoHandler(IOutboxMessage message) => LogNoHandler(message.Id, message.EventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox message {MessageId} has no handler for event type '{EventType}'.")]
    private partial void LogNoHandler(long messageId, string eventType);
}
