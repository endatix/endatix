using Endatix.Hosting.HealthChecks;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring health checks in the Endatix application.
/// </summary>
public class EndatixHealthChecksBuilder
{
    /// <summary>
    /// Name of the process-level check Endatix registers. Distinct from Aspire ServiceDefaults'
    /// own "self" check so both can coexist without a duplicate-name registration error.
    /// </summary>
    public const string SelfCheckName = "endatix-self";

    private readonly EndatixBuilder _parent;
    private bool _defaultsApplied;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndatixHealthChecksBuilder"/> class.
    /// </summary>
    /// <param name="parent">The parent builder.</param>
    public EndatixHealthChecksBuilder(EndatixBuilder parent)
    {
        _parent = parent;
        Builder = parent.Services.AddHealthChecks();
    }

    /// <summary>
    /// Configures health checks with default settings.
    /// Adds basic self-check by default.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksBuilder UseDefaults()
    {
        if (_defaultsApplied)
        {
            return this;
        }

        // Registered unconditionally, under a name Endatix owns. Previously this was skipped when
        // Aspire ServiceDefaults looked present, detected by a substring match on "ServiceDiscovery"
        // in any registered service type — which any transitive Microsoft.Extensions.ServiceDiscovery
        // reference satisfies. A false positive there left no check tagged self or live at all, and
        // an empty liveness predicate reports Healthy: a hung process would have stayed green
        // forever. Owning the name removes both the duplicate-name risk and the guesswork, and
        // guarantees the liveness probe always has something to evaluate.
        AddCheck(SelfCheckName, () => HealthCheckResult.Healthy(), tags: new[] { HealthCheckTags.Self });

        AddDbContextHealthChecks();

        _defaultsApplied = true;
        return this;
    }

    /// <summary>
    /// Adds a custom health check.
    /// </summary>
    /// <param name="name">The name of the health check.</param>
    /// <param name="check">A function that returns the health status.</param>
    /// <param name="failureStatus">The status to return when the health check fails.</param>
    /// <param name="tags">A list of tags that can be used to filter health checks.</param>
    /// <returns>The builder for chaining.</returns>
    public EndatixHealthChecksBuilder AddCheck(
        string name,
        Func<HealthCheckResult> check,
        FailureStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        // Create a custom health check implementation
        var healthCheck = new FuncHealthCheck(check);

        Builder.AddCheck(
            name,
            healthCheck,
            failureStatus?.ToHealthStatus() ?? HealthStatus.Unhealthy,
            tags ?? Array.Empty<string>());
        return this;
    }

    /// <summary>
    /// Gets the underlying health checks builder for advanced configuration.
    /// </summary>
    public IHealthChecksBuilder Builder { get; }

    /// <summary>
    /// Adds EF Core DbContext health checks for registered contexts (AppDbContext, AppIdentityDbContext).
    /// Only adds a check when the context is registered in the service collection (e.g. by Persistence.UseDefaults).
    /// </summary>
    private void AddDbContextHealthChecks()
    {
        if (_parent.Services.Any(s => s.ServiceType == typeof(AppDbContext)))
        {
            Builder.AddDbContextCheck<AppDbContext>("database", failureStatus: HealthStatus.Unhealthy, tags: new[] { HealthCheckTags.Db, HealthCheckTags.Ready });
        }

        if (_parent.Services.Any(s => s.ServiceType == typeof(AppIdentityDbContext)))
        {
            Builder.AddDbContextCheck<AppIdentityDbContext>("identity-database", failureStatus: HealthStatus.Unhealthy, tags: new[] { HealthCheckTags.Db, HealthCheckTags.Identity, HealthCheckTags.Ready });
        }
    }

    /// <summary>
    /// Completes configuration and returns to the parent builder.
    /// </summary>
    /// <returns>The parent builder for chaining.</returns>
    public EndatixBuilder Build() => _parent;
}

/// <summary>
/// Represents the failure status of a health check.
/// </summary>
public enum FailureStatus
{
    /// <summary>
    /// Indicates the health check has failed, but the failure doesn't compromise the service.
    /// </summary>
    Degraded,

    /// <summary>
    /// Indicates the health check has failed and the service is unhealthy.
    /// </summary>
    Unhealthy
}

/// <summary>
/// Extension methods for the <see cref="FailureStatus"/> enum.
/// </summary>
internal static class FailureStatusExtensions
{
    /// <summary>
    /// Converts a <see cref="FailureStatus"/> to a <see cref="HealthStatus"/>.
    /// </summary>
    /// <param name="status">The failure status.</param>
    /// <returns>The health status.</returns>
    public static HealthStatus ToHealthStatus(this FailureStatus status) => status switch
    {
        FailureStatus.Degraded => HealthStatus.Degraded,
        FailureStatus.Unhealthy => HealthStatus.Unhealthy,
        _ => HealthStatus.Unhealthy
    };
}

// Custom health check implementation that wraps a function
internal class FuncHealthCheck : IHealthCheck
{
    private readonly Func<HealthCheckResult> _check;

    public FuncHealthCheck(Func<HealthCheckResult> check)
    {
        _check = check;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_check());
    }
}