using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Fact]
    public async Task GetOrCreateAsync_GrantReadStartsAfterKeyLookup_KeepsOriginalTokenDeadline()
    {
        var cache = CreateInstance(false);
        var principal = CreatePrincipal();
        principal.Identities.Single().AddClaim(new("exp", _start.AddSeconds(30).ToUnixTimeSeconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture)));
        _clock.Now.Returns(_start, _start.AddSeconds(8));
        var current = await cache.GetOrCreateAsync(principal, _ => Task.FromResult(Result.Success(CreateData(default))),
            TestContext.Current.CancellationToken);

        current.ExpiresAt.Should().Be(_start.AddSeconds(20).UtcDateTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidateAsync_SharedStore_RetiresBothKeyFormsAcrossTenantsAndWarmHosts(bool global)
    {
        var writer = CreateInstance(true);
        var reader = CreateInstance(true);
        var subjects = new[] { new Subject("123", 42), new Subject("123", 43), new Subject("456", 42) };
        foreach (var subject in subjects)
        {
            await subject.WarmBothAsync(writer, _sharedL2);
            await subject.ReadBothAsync(reader);
            subject.Reads.Should().Be(2);
        }

        var ct = TestContext.Current.CancellationToken;
        if (global)
        {
            await writer.InvalidateAllAsync(ct);
        }
        else
        {
            await writer.InvalidateAsync("123", 42, ct);
        }

        foreach (var subject in subjects)
        {
            await subject.ReadBothAsync(reader);
            subject.Reads.Should().Be(global || subject.User == "123" ? 4 : 2);
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_RebuildFinishesAfterPeerInvalidation_CannotReplaceNewGeneration()
    {
        var oldHost = CreateInstance(true);
        var peer = CreateInstance(true);
        var ct = TestContext.Current.CancellationToken;
        var principal = CreatePrincipal();
        await _sharedL2.AfterWriteAsync(() => oldHost.GetOrCreateAsync(principal,
            _ => Task.FromResult(Result.Success(CreateData(_start.AddSeconds(1).UtcDateTime))), ct));
        _clock.Now.Returns(_start.AddSeconds(2));
        var rebuilding = new GrantRequest(oldHost, ct);
        var oldRead = rebuilding.TransformAsync();
        await AwaitAsync(rebuilding.Started.Task);
        try
        {
            await peer.InvalidateAsync("123", 42, ct);
            await _sharedL2.AfterWriteAsync(() => peer.GetOrCreateAsync(principal,
                _ => Task.FromResult(Result.Success(CreateData(default) with { Roles = [] })), ct));
        }
        finally
        {
            rebuilding.Finish.TrySetResult(CreateData(default));
            await AwaitAsync(oldRead);
        }
        var coldPeer = CreateInstance(true);
        var current = await coldPeer.GetOrCreateAsync(principal, _ => throw new InvalidOperationException("Expected L2 hit"), ct);
        current.Roles.Should().BeEmpty();
    }

    private sealed record Subject(string User, long Tenant)
    {
        internal int Reads { get; private set; }
        private ClaimsPrincipal Principal => new(new ClaimsIdentity(
            [new(ClaimNames.UserId, User), new(ClaimNames.TenantId, Tenant.ToString(System.Globalization.CultureInfo.InvariantCulture))], "test"));

        internal async Task WarmBothAsync(AuthorizationCache cache, SharedDistributedCache l2)
        {
            await l2.AfterWriteAsync(() => ByPrincipalAsync(cache));
            await l2.AfterWriteAsync(() => ByUserAsync(cache));
        }

        internal async Task ReadBothAsync(AuthorizationCache cache)
        {
            await ByPrincipalAsync(cache);
            await ByUserAsync(cache);
        }

        private Task<AuthorizationData> ByPrincipalAsync(AuthorizationCache cache) =>
            cache.GetOrCreateAsync(Principal, ReadAsync, TestContext.Current.CancellationToken);

        private Task<AuthorizationData> ByUserAsync(AuthorizationCache cache) =>
            cache.GetOrCreateAsync(User, Tenant, null, ReadAsync, TestContext.Current.CancellationToken);

        private Task<Result<AuthorizationData>> ReadAsync(CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Result.Success(CreateData(default) with { UserId = User, TenantId = Tenant }));
        }
    }
}
