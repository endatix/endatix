namespace Endatix.Hosting.HealthChecks;

/// <summary>
/// Tags Endatix puts on its health checks, and which the probe endpoints filter on.
/// </summary>
/// <remarks>
/// Shared constants rather than literals at each site: registration and predicate must agree, and a
/// rename that touched only one of them would leave a probe with an empty predicate — which reports
/// Healthy, so it would fail silently rather than loudly.
/// </remarks>
public static class HealthCheckTags
{
    /// <summary>Process-level check. Selected by the liveness probe.</summary>
    public const string Self = "self";

    /// <summary>Aspire ServiceDefaults' equivalent of <see cref="Self"/>. Also selected by liveness.</summary>
    public const string Live = "live";

    /// <summary>A dependency the instance needs to serve traffic. Selected by the readiness probe.</summary>
    public const string Ready = "ready";

    /// <summary>Database-backed check.</summary>
    public const string Db = "db";

    /// <summary>Identity store check.</summary>
    public const string Identity = "identity";
}
