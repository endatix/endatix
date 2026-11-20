using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions;

/// <summary>
/// The entitlement names <see cref="ITenantEntitlements"/> answers for.
/// </summary>
/// <remarks>
/// The values are stored and compared by hosts that grant entitlements, so they must not change, even while the
/// API is experimental (<c>ENDATIX0001</c>): only its C# signatures may.
/// </remarks>
[Experimental("ENDATIX0001")]
public static class EntitlementNames
{
    /// <summary>
    /// The tenant may set its own branding (logo, icon, accent).
    /// </summary>
    public const string BRANDING = "branding";
}
