namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Identifies where a flag is evaluated. Independent of <see cref="FeatureFlagClass"/>: the class says
/// which system answers, the scope says when the question is asked and on whose behalf.
/// </summary>
public enum FeatureFlagScope
{
    /// <summary>
    /// Evaluated once at host startup, with no evaluation context. There is no tenant and no user at
    /// that point, so a deployment-scoped flag must never carry targeting.
    /// </summary>
    Deployment = 1,

    /// <summary>
    /// Evaluated per request, against a context carrying the current tenant and user.
    /// </summary>
    Tenant = 2,
}
