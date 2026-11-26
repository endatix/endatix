using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Internal helper class for managing the authorization cache.
/// </summary>
/// <remarks>
/// Every key carries this deployment's prefix and the access versions of the request's tenant and of system roles,
/// read uncached on each lookup. A role change writes a new access version for its tenant as the last statement of its
/// transaction, so every API instance misses its old entries on the next request. Explicit eviction also retires
/// in-flight writes in the backing cache. An entry lives at most 60 seconds and ends 10 seconds before its token (one
/// second at least), and is never served past the earliest expiry of the role permissions it holds.
/// </remarks>
internal class AuthorizationCache(
    AuthorizationCacheStorage storage,
    IDateTimeProvider dateTimeProvider,
    AuthorizationTokenFingerprint tokenFingerprint,
    IAccessVersionStore accessVersionStore,
    RequiredActionEvaluation actions) : IAuthorizationCache
{
    /// <summary>The longest an entry lives, which bounds a change that skips the save hook.</summary>
    private static readonly TimeSpan _maxEntryLifetime = TimeSpan.FromSeconds(60);

    // Cache key prefixes
    private const string JWT_AUTH_DATA_CACHE_KEY_PREFIX = "jwt_auth";
    private const string USER_AUTH_DATA_CACHE_KEY_PREFIX = "usr_auth";

    // Cache tags
    private const string ALL_AUTH_DATA_TAG = "auth_data:all";

    private static readonly TimeSpan _cacheExpiryBuffer = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _minEntryLifetime = TimeSpan.FromSeconds(1);

    Task<AuthorizationData> IAuthorizationCache.GetOrCreateAsync(ClaimsPrincipal principal,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken) =>
        GetOrCreateAsync(principal, async token => SessionAuthorization.WithoutProof(await dataFactory(token), principal), cancellationToken);

    Task<AuthorizationData> IAuthorizationCache.GetOrCreateAsync(string userId, long tenantId, ClaimsPrincipal? principal,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken) =>
        GetOrCreateAsync(userId, tenantId, principal, async token => SessionAuthorization.WithoutProof(await dataFactory(token), principal), cancellationToken);

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(ClaimsPrincipal principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId() ?? throw new InvalidOperationException("Principal must have a user ID");
        var tenantId = principal.GetTenantId();
        var cacheKeyTokenPart = tokenFingerprint.For(principal, userId, tenantId);
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        var entry = new CacheEntry(GetClaimsCacheKey(versions, cacheKeyTokenPart), EntryLifetime(principal), GetAuthDataCacheTags(userId, tenantId), versions, actions.ConfigurationKey, tenantId);
        entry = entry.InGeneration(await storage.GenerationAsync(userId, cancellationToken));
        return principal.HasActorClaim() ? await ReadActorSessionAsync(entry, dataFactory, cancellationToken)
            : await GetOrCreateEntryAsync(entry, dataFactory, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(string userId, long tenantId, ClaimsPrincipal? principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        var entry = new CacheEntry(GetUserAuthDataCacheKey(versions, userId, tenantId), EntryLifetime(principal), GetAuthDataCacheTags(userId, tenantId), versions, actions.ConfigurationKey, tenantId);
        entry = entry.InGeneration(await storage.GenerationAsync(userId, cancellationToken));
        return principal?.HasActorClaim() == true
            ? await ReadActorSessionAsync(entry with { Key = $"{entry.Key}:{tokenFingerprint.For(principal, userId, tenantId)}" }, dataFactory, cancellationToken)
            : await GetOrCreateEntryAsync(entry, dataFactory, cancellationToken);
    }

    private async Task<AuthorizationData> ReadActorSessionAsync(CacheEntry entry,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken ct)
    {
        // The credential names the actor, not the persisted home. Verify before selecting even a warm snapshot.
        var verified = await dataFactory(ct);
        ct.ThrowIfCancellationRequested();
        if (!verified.IsSuccess)
        {
            throw new InvalidOperationException("Failed to verify session authorization.");
        }
        var proof = verified.Value.VerifiedAssumption;
        var homeVersion = proof is null ? null : (await accessVersionStore.ReadAsync(proof.HomeTenantId, ct)).Versions;
        var key = $"{entry.Key}:actor-session:{verified.Value.UserId}:{verified.Value.TenantId}:{verified.Value.UnverifiedActor}:{proof?.ActorUserId}:{proof?.HomeTenantId}:{homeVersion}";
        var snapshot = await GetOrCreateEntryAsync(entry with { Key = key, Configuration = $"{entry.Configuration}:{homeVersion}" },
            new SessionRead(verified, dataFactory).BuildAsync, ct);
        return snapshot with { VerifiedAssumption = proof };
    }

    private sealed class SessionRead(Result<AuthorizationData> verified, Func<CancellationToken, Task<Result<AuthorizationData>>> factory)
    {
        private bool _first = true;

        internal async Task<Result<AuthorizationData>> BuildAsync(CancellationToken ct)
        {
            if (_first)
            {
                _first = false;
                return verified;
            }
            var fresh = await factory(ct);
            return fresh.IsSuccess && SameSession(verified.Value, fresh.Value) ? fresh : Result.Forbidden();
        }

        private static bool SameSession(AuthorizationData expected, AuthorizationData fresh) =>
            expected.UserId == fresh.UserId && expected.TenantId == fresh.TenantId && expected.VerifiedAssumption == fresh.VerifiedAssumption
            && expected.UnverifiedActor == fresh.UnverifiedActor;
    }

    /// <summary>
    /// Serves the entry, building it on a miss. An entry past its own <see cref="AuthorizationData.ExpiresAt"/> (a role
    /// permission it holds has lapsed) counts as a miss: HybridCache fixes an entry's lifetime before the data is
    /// built, so the data carries the shorter bound itself.
    /// </summary>
    private async Task<AuthorizationData> GetOrCreateEntryAsync(
        CacheEntry entry,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken)
    {
        var cached = await ReadCachedAsync(entry, cancellationToken);
        if (cached is not null && cached.ExpiresAt > dateTimeProvider.Now.UtcDateTime
            && !cached.RequiredActionsUnavailable && !NeedsHydratedVersion(entry, cached))
        {
            return cached.ExpiresAt > entry.Window.End ? cached with { ExpiresAt = entry.Window.End } : cached;
        }
        var snapshot = await BuildAsync(entry, dataFactory, cancellationToken);
        var current = await PublishAsync(entry, new(snapshot, cached), cancellationToken);
        if (current is not null)
        {
            return current;
        }
        snapshot = await BuildAsync(entry, dataFactory, cancellationToken);
        return snapshot.WithCurrentPermissions(dateTimeProvider.Now.UtcDateTime);
    }

    private ValueTask<AuthorizationData?> ReadCachedAsync(CacheEntry entry, CancellationToken ct) =>
        storage.Entries.GetOrCreateAsync(entry.Key, 0, static (_, _) => ValueTask.FromResult<AuthorizationData?>(null),
            CacheEntry.ReadOnly, entry.Tags, ct);

    private sealed record Publication(BuiltSnapshot Snapshot, AuthorizationData? Previous);

    private async ValueTask<AuthorizationData?> PublishAsync(CacheEntry entry, Publication publication, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = publication.Snapshot;
        var current = snapshot.At(dateTimeProvider.Now.UtcDateTime);
        if (current.ExpiresAt > dateTimeProvider.Now.UtcDateTime)
        {
            if (publication.Previous is null)
            {
                // Another cold reader may already have published. Offer only a completed value.
                await storage.Entries.GetOrCreateAsync(entry.Key, _ => ValueTask.FromResult(current), entry.Options, entry.Tags, ct);
            }
            else
            {
                await storage.Entries.SetAsync(entry.Key, current, entry.Options, entry.Tags, ct);
            }
        }
        return snapshot.AfterPublication(current, dateTimeProvider.Now.UtcDateTime);
    }

    private async Task<BuiltSnapshot> BuildAsync(CacheEntry entry, Func<CancellationToken, Task<Result<AuthorizationData>>> factory,
        CancellationToken ct)
    {
        var started = dateTimeProvider.Now.UtcDateTime;
        var result = await factory(ct);
        ct.ThrowIfCancellationRequested();
        if (result is null || !result.IsSuccess)
        {
            throw new InvalidOperationException(result?.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
        }
        var data = DetachGrants(result.Value);
        var subjectEntry = NeedsHydratedVersion(entry, data)
            ? entry with { Versions = await accessVersionStore.ReadAsync(data.TenantId, ct) } : entry;
        var project = await actions.EvaluateAsync(data, subjectEntry, ct);
        ct.ThrowIfCancellationRequested();
        return new(data.ExpiresAt, at => Stamp(project(at), started, entry.Window.End));
    }

    private static AuthorizationData DetachGrants(AuthorizationData data) => data with
    {
        Roles = new(data.Roles, data.Roles.Comparer),
        Permissions = new(data.Permissions, data.Permissions.Comparer),
        RequiredActions = data.RequiredActions.Select(action => action with { ResolverRoles = action.ResolverRoles.ToArray() }).ToArray()
    };

    // External authorization strategies can map a positive token tenant to a different local tenant.
    private bool NeedsHydratedVersion(CacheEntry entry, AuthorizationData data) =>
        actions.HasProviders && entry.LookupTenant > 0 && entry.LookupTenant != data.TenantId;

    /// <summary>One cache entry's key, lifetime, tags and version scope.</summary>
    internal readonly record struct CacheEntry(string Key, Lifetime Window, string[] Tags, AccessVersionKey Versions, string Configuration, long LookupTenant)
    {
        internal static HybridCacheEntryOptions ReadOnly { get; } = new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData };
        internal CacheEntry InGeneration(string generation) => this with { Key = $"{Key}:{generation}", Configuration = $"{Configuration}:{generation}" };
        /// <summary>The entry lives <see cref="Window"/> in both cache levels.</summary>
        public HybridCacheEntryOptions Options => new() { Expiration = Window.Duration, LocalCacheExpiration = Window.Duration };
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string userId, long tenantId, CancellationToken cancellationToken)
    {
        await storage.Entries.RemoveByTagAsync(GetUserAuthDataTag(userId), cancellationToken);
        await storage.Entries.RemoveByTagAsync(GetUserTenantAuthDataTag(userId, tenantId), cancellationToken);
        await storage.RetireUserAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateAllAsync(CancellationToken cancellationToken)
    {
        await storage.Entries.RemoveByTagAsync(AllAuthDataCacheTags, cancellationToken);
        await storage.RetireAllAsync(cancellationToken);
    }

    /// <summary>Per-token key; the access versions in it retire every entry when an authorization input changes.</summary>
    private string GetClaimsCacheKey(AccessVersionKey versions, string tokenPart) =>
        $"{CacheNamespace(versions, JWT_AUTH_DATA_CACHE_KEY_PREFIX)}:{tokenPart}";

    /// <summary>Per-user-and-tenant key; the access versions in it retire every entry when an authorization input changes.</summary>
    private string GetUserAuthDataCacheKey(AccessVersionKey versions, string userId, long tenantId) =>
        $"{CacheNamespace(versions, USER_AUTH_DATA_CACHE_KEY_PREFIX)}:{userId}:{tenantId}";

    private string CacheNamespace(AccessVersionKey versions, string kind) =>
        $"{versions.Prefix}:{kind}:{versions.Versions}.{actions.ConfigurationKey}";

    private static string GetUserAuthDataTag(string userId) => $"auth_data:user:{userId}";

    private static string GetUserTenantAuthDataTag(string userId, long tenantId) => $"auth_data:user:{userId}:tenant:{tenantId}";

    // Cache tag for all authorization data
    private static string[] AllAuthDataCacheTags => [ALL_AUTH_DATA_TAG];

    private static string[] GetAuthDataCacheTags(string userId, long tenantId) =>
    [
        ALL_AUTH_DATA_TAG,
        GetUserAuthDataTag(userId),
        GetUserTenantAuthDataTag(userId, tenantId)
    ];

    /// <summary>
    /// How long an entry lives: 60 seconds, or less when the principal's token expires sooner. The entry ends 10 seconds
    /// before the token; a token already inside that buffer gets a one-second entry, never a full one.
    /// </summary>
    internal readonly record struct Lifetime(DateTime Start, TimeSpan Duration)
    {
        internal DateTime End => Start.Add(Duration);
    }

    private Lifetime EntryLifetime(ClaimsPrincipal? principal)
    {
        var start = dateTimeProvider.Now;
        var expClaim = principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (expClaim is null || !long.TryParse(expClaim, out var expirySeconds))
        {
            return new(start.UtcDateTime, _maxEntryLifetime);
        }

        var timeUntilExpiry = DateTimeOffset.FromUnixTimeSeconds(expirySeconds) - start - _cacheExpiryBuffer;
        return new(start.UtcDateTime, TimeSpan.FromTicks(Math.Clamp(timeUntilExpiry.Ticks, _minEntryLifetime.Ticks, _maxEntryLifetime.Ticks)));
    }

    private const short ETAG_LENGTH = 12;

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

    private sealed record BuiltSnapshot(DateTime PermissionExpiry, Func<DateTime, AuthorizationData> At)
    {
        internal AuthorizationData? AfterPublication(AuthorizationData published, DateTime now)
        {
            if (PermissionsExpired(now))
            {
                return null;
            }
            return published.ExpiresAt > now ? published : At(now);
        }

        internal bool PermissionsExpired(DateTime now) => PermissionExpiry != default && PermissionExpiry <= now;
        internal AuthorizationData WithCurrentPermissions(DateTime now) => PermissionsExpired(now)
            ? throw new InvalidOperationException("Fresh authorization data contains expired permissions.") : At(now);
    }
}
