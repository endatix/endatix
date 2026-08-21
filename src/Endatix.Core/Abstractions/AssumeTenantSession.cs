namespace Endatix.Core.Abstractions;

/// <summary>
/// Defaults for assume-tenant sessions. Assumed access tokens are shorter-lived than login tokens.
/// </summary>
public static class AssumeTenantSession
{
    public const int AccessExpiryMinutes = 15;
}
