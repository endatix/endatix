using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Identity.Authorization;

internal partial class AuthorizationCache
{
    // A shared callback owns detached inputs and never resolves a service from the request scope.
    private sealed class GrantFill(Grants grants, RequiredActionEvaluation actions, IDateTimeProvider clock)
    {
        private const short ETAG_LENGTH = 12;
        private static readonly HybridCacheEntryOptions _inFlightOnly = new()
        {
            Flags = HybridCacheEntryFlags.DisableLocalCache | HybridCacheEntryFlags.DisableDistributedCache
        };
        internal AuthorizationData? Built { get; private set; }
        private DateTime Now => clock.Now.UtcDateTime;

        internal async ValueTask<AuthorizationData> BuildAsync(CancellationToken ct) => Built = (await EvaluateAsync(ct)).At(Now);

        private string RefreshKey => field ??= $"{grants.Entry.Key}:refresh:{grants.Entry.Versions.Versions}:{GrantFingerprint()}";

        private string GrantFingerprint()
        {
            var data = grants.Data;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                data.UserId, data.TenantId, data.ExpiresAt, data.IsAdmin, data.UnverifiedActor,
                Roles = data.Roles.Order(StringComparer.Ordinal), Permissions = data.Permissions.Order(StringComparer.Ordinal),
                data.RequiredActions, data.RequiredActionsUnavailable, data.RequiredActionsEnforced
            });
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        internal async Task<AuthorizationData?> RefreshAsync(HybridCache cache, CancellationToken ct)
        {
            var snapshot = await cache.GetOrCreateAsync(RefreshKey, token => PublishAsync(cache, token), _inFlightOnly, cancellationToken: ct);
            var now = Now;
            return snapshot.PermissionsExpired(now) ? null : snapshot.At(now);
        }

        private async ValueTask<BuiltSnapshot> PublishAsync(HybridCache cache, CancellationToken ct)
        {
            var snapshot = await EvaluateAsync(ct);
            var rebuilt = snapshot.At(Now);
            if (rebuilt.ExpiresAt > Now)
            {
                await cache.SetAsync(grants.Entry.Key, rebuilt, grants.Entry.Options, grants.Entry.Tags, ct);
            }
            return snapshot;
        }

        internal async Task<AuthorizationData> WithCurrentPermissionsAsync(HybridCache cache, CancellationToken ct)
        {
            var snapshot = await cache.GetOrCreateAsync($"{RefreshKey}:final", EvaluateAsync, _inFlightOnly, cancellationToken: ct);
            return snapshot.WithCurrentPermissions(Now);
        }

        private async ValueTask<BuiltSnapshot> EvaluateAsync(CancellationToken ct)
        {
            var project = await actions.EvaluateAsync(grants.Data, grants.Entry, ct);
            ct.ThrowIfCancellationRequested();
            return new(grants.Data.ExpiresAt, at => Stamp(project(at), grants.Started, grants.Entry.Window.End));
        }

        private static AuthorizationData Stamp(AuthorizationData data, DateTime builtAt, DateTime deadline)
        {
            var expiresAt = EarlierOf(deadline, data.ExpiresAt);
            expiresAt = EarlierOf(expiresAt, RequiredActionEvaluation.NextTransition(data, builtAt));
            return data.WithCacheMetadata(builtAt, expiresAt, GenerateETag(data));
        }

        /// <summary>The entry's end, or the earliest permission expiry the data carries when that comes first.</summary>
        private static DateTime EarlierOf(DateTime entryEnd, DateTime permissionsExpireAt) =>
            permissionsExpireAt != default && permissionsExpireAt < entryEnd ? permissionsExpireAt : entryEnd;

        /// <summary>
        /// Generates an ETag for authorization data based on its content.
        /// Centralized ETag generation ensures consistency across all strategies.
        /// </summary>
        private static string GenerateETag(AuthorizationData data)
        {
            var sortedRoles = string.Join(",", data.Roles.OrderBy(r => r).ToArray());
            var sortedPermissions = string.Join(",", data.Permissions.OrderBy(p => p).ToArray());
            var content = $"{data.UserId}:{data.TenantId}:{sortedRoles}:{sortedPermissions}";
            if (data.RequiredActionsUnavailable)
            {
                content += ":standing-unavailable";
            }
            if (data.RequiredActions.Count > 0)
            {
                content += ":" + JsonSerializer.Serialize(data.RequiredActions);
            }
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

            var shortETag = Convert.ToBase64String(hash)[..ETAG_LENGTH];
            return shortETag;
        }
    }

    // Only shared in flight on this host; the stored value is always AuthorizationData.
    [System.ComponentModel.ImmutableObject(true)]
    internal sealed record BuiltSnapshot(DateTime PermissionExpiry, Func<DateTime, AuthorizationData> At)
    {
        internal bool PermissionsExpired(DateTime now) => PermissionExpiry != default && PermissionExpiry <= now;
        internal AuthorizationData WithCurrentPermissions(DateTime now) => PermissionsExpired(now)
            ? throw new InvalidOperationException("Fresh authorization data contains expired permissions.") : At(now);
    }
}
