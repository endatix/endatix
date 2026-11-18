namespace Endatix.Core.Abstractions;

/// <summary>
/// The entitlement names <see cref="ITenantEntitlements"/> answers for.
/// </summary>
/// <remarks>
/// The values are stored and compared by hosts that grant entitlements, so they must not change.
/// </remarks>
public static class Entitlements
{
    /// <summary>
    /// The tenant may set its own branding (logo, icon, accent).
    /// </summary>
    public const string BRANDING = "branding";
}
