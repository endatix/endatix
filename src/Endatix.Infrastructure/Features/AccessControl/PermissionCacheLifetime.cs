using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Caching;

namespace Endatix.Infrastructure.Features.AccessControl;

internal readonly struct PermissionCacheLifetime(AuthorizationData snapshot, DateTime now)
{
    internal TimeSpan Ttl { get; } = snapshot.ComputeAuthTtl(now);

    // A refreshed snapshot may shorten this response without changing the cached projection.
    internal Cached<T> Cap<T>(Cached<T> entry) where T : class =>
        snapshot.ExpiresAt != default && snapshot.ExpiresAt < entry.ExpiresAt
            ? entry with { ExpiresAt = snapshot.ExpiresAt } : entry;
}
