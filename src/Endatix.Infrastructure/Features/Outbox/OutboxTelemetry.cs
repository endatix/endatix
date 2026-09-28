namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// The meter and tracing source the outbox relay's delivery to the job queue reports on.
/// </summary>
public static class OutboxTelemetry
{
    /// <summary>A telemetry pipeline sees outbox delivery only once it subscribes to this name.</summary>
    public const string SourceName = "Endatix.Outbox";
}
