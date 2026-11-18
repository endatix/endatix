using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// The default <see cref="ITenantEntitlements"/>: every tenant holds every entitlement.
/// </summary>
public sealed class AllowAllTenantEntitlements : ITenantEntitlements
{
    /// <inheritdoc />
    public Task<bool> HasAsync(long tenantId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult(true);
    }
}
