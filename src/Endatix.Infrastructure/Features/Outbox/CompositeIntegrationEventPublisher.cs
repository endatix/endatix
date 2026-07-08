using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Fans a claimed outbox row out to every registered <see cref="IOutboxIntegrationEventHandler"/>
/// whose <see cref="IOutboxIntegrationEventHandler.EventTypes"/> includes the message event type.
/// </summary>
internal sealed class CompositeIntegrationEventPublisher(
    IEnumerable<IOutboxIntegrationEventHandler> handlers,
    ILogger<CompositeIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    private readonly IOutboxIntegrationEventHandler[] _handlers = handlers.ToArray();

    /// <inheritdoc />
    public async Task PublishAsync(IOutboxMessage message, CancellationToken cancellationToken)
    {
        var matched = _handlers
            .Where(handler => handler.EventTypes.Contains(message.EventType, StringComparer.Ordinal))
            .ToArray();

        if (matched.Length == 0)
        {
            logger.LogInformation(
                "Outbox message {MessageId} has no handler for event type '{EventType}'.",
                message.Id,
                message.EventType);
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
                logger.LogError(
                    ex,
                    "Outbox handler {HandlerType} failed for message {MessageId} ({EventType}).",
                    handler.GetType().Name,
                    message.Id,
                    message.EventType);
                throw;
            }
        }
    }
}
