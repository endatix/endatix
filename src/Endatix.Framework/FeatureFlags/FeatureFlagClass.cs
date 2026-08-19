namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Identifies which system is the record for a flag's value, and therefore which provider resolves it.
/// </summary>
/// <remarks>
/// There is deliberately no member for config-driven flags. A switch whose answer has no record outside
/// the deployment is configuration, not a feature flag, and belongs in <c>appsettings</c> or an
/// environment variable. The enum starts at 1 so a definition cannot acquire a class by default.
/// </remarks>
public enum FeatureFlagClass
{
    /// <summary>
    /// A gradual release or opt-in beta. The answer does not exist anywhere until the rollout provider
    /// buckets a subject, which is why that provider is the record for it.
    /// </summary>
    Rollout = 1,

    /// <summary>
    /// A statement about what a customer is entitled to. Resolved from the licence file or the contract
    /// service, never from the rollout provider, and never enabled by configuration alone.
    /// </summary>
    Entitlement = 2,
}
