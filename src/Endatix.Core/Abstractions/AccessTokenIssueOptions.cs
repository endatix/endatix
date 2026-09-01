namespace Endatix.Core.Abstractions;

/// <summary>
/// Options for minting an access token. Assume-tenant sets target TenantId and ActorUserId.
/// </summary>
public sealed record AccessTokenIssueOptions(
    long TenantId,
    long? ActorUserId = null,
    int? AccessExpiryMinutes = null,
    string? Audience = null);
