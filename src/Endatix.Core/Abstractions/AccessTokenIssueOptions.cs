namespace Endatix.Core.Abstractions;

/// <summary>
/// Options for minting an access token. Login uses the user's home tenant with no actor claim.
/// Assume-tenant supplies a target <see cref="TenantId"/> and <see cref="ActorUserId"/>.
/// </summary>
public sealed record AccessTokenIssueOptions(
    long TenantId,
    long? ActorUserId = null,
    int? AccessExpiryMinutes = null,
    string? Audience = null);
