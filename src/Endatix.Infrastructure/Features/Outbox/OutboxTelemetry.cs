namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// The meter and tracing source the outbox relay's delivery to the job queue reports on.
/// </summary>
public static class OutboxTelemetry
{
    /// <summary>A telemetry pipeline sees outbox delivery only once it subscribes to this name.</summary>
    public const string SourceName = "Endatix.Outbox";

    /// <summary>The tag the fan-out's span and job counter carry the outbox event type in.</summary>
    internal const string EventTypeTag = "endatix.outbox.event_type";
}
