namespace Endatix.Core.Abstractions;

/// <summary>
/// Answers whether a tenant may use a capability that a plan or licence can grant, such as
/// <see cref="EntitlementNames.BRANDING"/>.
/// </summary>
/// <remarks>
/// <para>
/// The default answers yes to every entitlement, so a self-hosted deployment has every feature and its
/// licence governs paid use. A host replaces the default by registering its own implementation.
/// A new paid capability adds a name to <see cref="EntitlementNames"/>, not a method here.
/// </para>
/// <para>
/// This is the per-tenant answer for a capability. A feature flag of class
/// <c>FeatureFlagClass.Entitlement</c> is a switch evaluated by the feature flag system; a capability that a
/// tenant's plan grants is asked here, alongside any rollout flag that still gates it.
/// </para>
/// <para>
/// Names are compared ordinally and case-sensitively. An implementation that grants by plan denies a name it
/// does not know. When the lookup fails, an implementation throws rather than answering either way, and a
/// caller does not turn that failure into an answer. A scoped implementation may cache its answers for the scope.
/// </para>
/// <para>
/// The question is always about one tenant. An entitlement of the whole deployment, read at startup with no tenant,
/// is not asked here.
/// </para>
/// </remarks>
public interface ITenantEntitlements
{
    /// <summary>
    /// Returns whether the tenant holds the named entitlement.
    /// </summary>
    /// <param name="tenantId">The tenant to ask about.</param>
    /// <param name="name">The entitlement name, one of <see cref="EntitlementNames"/>.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns><c>true</c> when the tenant may use the capability.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or white space.</exception>
    Task<bool> IsEntitledAsync(long tenantId, string name, CancellationToken cancellationToken = default);
}
