using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Handles a claimed outbox row for one or more integration <see cref="IOutboxMessage.EventType"/> values.
/// Multiple handlers may subscribe to the same event type; the composite publisher invokes all matches.
/// </summary>
public interface IOutboxIntegrationEventHandler
{
    /// <summary>
    /// The event types this handler subscribes to.
    /// </summary>
    IReadOnlyCollection<string> EventTypes { get; }


    /// <summary>
    /// Handles a claimed outbox row for one or more integration <see cref="IOutboxMessage.EventType"/> values.
    /// Multiple handlers may subscribe to the same event type; the composite publisher invokes all matches.
    /// In case of an error, the handler should throw an exception. The composite publisher will retry the message.
    /// </summary>
    Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken);
}
