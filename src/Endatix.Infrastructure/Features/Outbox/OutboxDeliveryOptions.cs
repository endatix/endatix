namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// How the outbox relay delivers, bound from <c>Endatix:Outbox</c> and read once at startup.
/// </summary>
public sealed class OutboxDeliveryOptions
{
    public const string SectionName = "Endatix:Outbox";

    /// <summary>
    /// Delivers each message by enqueuing one background job per subscriber, instead of running every subscriber
    /// inside the relay. Needs the Jobs module (PostgreSQL only); with the module off the relay pauses rather than
    /// lose messages. Every inline outbox handler must have a matching job subscription, or startup fails. Off by
    /// default.
    /// </summary>
    public bool DeliverToJobQueue { get; set; }
}
