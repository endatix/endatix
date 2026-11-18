namespace Endatix.Core.Abstractions;

/// <summary>
/// Answers whether a tenant may use a capability that a plan or licence can grant, such as
/// <see cref="Entitlements.BRANDING"/>.
/// </summary>
/// <remarks>
/// The default answers yes to every entitlement, so a self-hosted deployment has every feature and its
/// licence governs paid use. A host replaces the default by registering its own implementation.
/// A new paid capability adds a name to <see cref="Entitlements"/>, not a method here.
/// </remarks>
public interface ITenantEntitlements
{
    /// <summary>
    /// Returns whether the tenant holds the named entitlement.
    /// </summary>
    /// <param name="tenantId">The tenant to ask about.</param>
    /// <param name="name">The entitlement name, one of <see cref="Entitlements"/>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns><c>true</c> when the tenant may use the capability.</returns>
    Task<bool> HasAsync(long tenantId, string name, CancellationToken cancellationToken = default);
}
