using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Domain;

/// <summary>
/// The one place a <see cref="JobScope"/> meets the <c>TenantId</c> column: a tenant job stores its tenant, a system
/// job stores <see cref="SystemTenantId"/>.
/// </summary>
/// <remarks>
/// The system value is zero, the value an app-level outbox row stores for "no tenant", so a system row is hidden by
/// the tenant filter from every tenant. No other code compares a job's tenant to it.
/// </remarks>
internal static class JobScopeStorage
{
    /// <summary>The stored tenant of a system job.</summary>
    public const long SystemTenantId = 0;

    public static long ToStorage(JobScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.TenantId ?? SystemTenantId;
    }

    /// <exception cref="InvalidOperationException">The stored value names neither a tenant nor the system.</exception>
    public static JobScope FromStorage(long tenantId) => tenantId switch
    {
        SystemTenantId => JobScope.System,
        > 0 => JobScope.ForTenant(tenantId),
        _ => throw new InvalidOperationException($"A background job row stores tenant {tenantId}, which is no scope."),
    };
}
