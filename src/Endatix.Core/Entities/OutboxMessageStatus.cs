namespace Endatix.Core.Entities;

/// <summary>
/// Lifecycle state of an <see cref="OutboxMessage"/> as it moves through the relay.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Captured, awaiting publication.</summary>
    Pending = 0,

    /// <summary>Successfully published to the broker.</summary>
    Sent = 1,

    /// <summary>Exhausted the maximum publish attempts; needs operator attention.</summary>
    Failed = 2,
}
