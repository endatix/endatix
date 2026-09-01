namespace Endatix.Core.Abstractions;

/// <summary>
/// Options for minting an access token. See ARCHITECTURE.md (JWT session).
/// TenantId is always the session tid. ActorUserId set → act claim (assume-tenant).
/// </summary>
public sealed record AccessTokenIssueOptions(
    long TenantId,
    long? ActorUserId = null,
    int? AccessExpiryMinutes = null,
    string? Audience = null);
