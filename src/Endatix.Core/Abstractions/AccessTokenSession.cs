namespace Endatix.Core.Abstractions;

/// <summary>
/// Claims read from a validated Endatix access token.
/// </summary>
public sealed record AccessTokenSession(long UserId, long TenantId, long? ActorUserId);
