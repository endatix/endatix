using System.Collections.Frozen;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Outbox event types Endatix no longer raises and no consumer may receive. A row of one fails instead of being marked
/// sent, so a leftover row never reaches a handler that now means something else by its subscription. Drain the outbox
/// before upgrading; a row left behind retries until it dead-letters, with the reason in the log.
/// </summary>
internal static class RetiredEventTypes
{
    /// <summary>The reason a retired row fails.</summary>
    public const string Reason = "retired event type";

    private static readonly FrozenSet<string> _retired = new[]
    {
        // Replaced by the phase-named submission lifecycle events (submission.closed for a screen-out).
        "submission.collection_status_changed",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Every retired event type.</summary>
    public static IReadOnlyCollection<string> All => _retired;

    public static bool IsRetired(string eventType) => _retired.Contains(eventType);

    /// <summary>Throws for a retired row, before any handler or subscription is matched.</summary>
    /// <exception cref="RetiredEventTypeException">The row's event type is retired.</exception>
    public static void EnsureNotRetired(IOutboxMessage message)
    {
        if (IsRetired(message.EventType))
        {
            throw new RetiredEventTypeException(message.EventType, message.Id);
        }
    }
}

/// <summary>An outbox row of a retired event type. Not delivered: it retries until it dead-letters.</summary>
public sealed class RetiredEventTypeException(string eventType, long messageId)
    : InvalidOperationException($"{RetiredEventTypes.Reason}: {eventType} (outbox message {messageId})")
{
    /// <summary>The retired event type.</summary>
    public string EventType { get; } = eventType;
}
