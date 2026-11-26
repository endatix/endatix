using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_IdenticalGrantsWithoutTokenId_KeepDistinctSessionReports(bool byUser)
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(call => [SessionAction(call.Arg<RequiredActionSubject>().VerifiedAssumption)]);
        var writer = CreateInstance(true, provider);
        var reader = CreateInstance(true, provider);

        var member = await _sharedL2.AfterWriteAsync(() => ReadSessionAsync(writer, null, byUser));
        var firstHome = await _sharedL2.AfterWriteAsync(() => ReadSessionAsync(writer, new(123, 1), byUser));
        var secondHome = await _sharedL2.AfterWriteAsync(() => ReadSessionAsync(writer, new(123, 2), byUser));
        var distributed = await ReadSessionAsync(reader, new(123, 2), byUser);

        member.RequiredActions.Single().Key.Should().Be("member");
        firstHome.RequiredActions.Single().Key.Should().Be("home-1");
        secondHome.RequiredActions.Single().Key.Should().Be("home-2");
        distributed.VerifiedAssumption.Should().Be(new VerifiedAssumption(123, 2));
        distributed.RequiredActions.Single().CanResolve.Should().BeFalse();
        await provider.Received(3).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateAsync_AssumedPermissionExpiresDuringWrite_ReadsCurrentGrants()
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.CacheFor.Returns(TimeSpan.FromSeconds(5));
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(true, provider);
        var principal = CreatePrincipal();
        principal.Identities.Single().AddClaim(new(ClaimNames.Actor, "123"));
        var expiry = _start.AddSeconds(10);
        var initial = CreateData(expiry.UtcDateTime) with { VerifiedAssumption = new(123, 1) };
        Task<AuthorizationData> ReadAsync() => cache.GetOrCreateAsync(principal,
            _ => Task.FromResult(Result.Success(_clock.Now < expiry ? initial : initial with { Permissions = [], ExpiresAt = default })),
            TestContext.Current.CancellationToken);
        (await _sharedL2.AfterWriteAsync(ReadAsync)).Permissions.Should().Contain("forms.view");
        _clock.Now.Returns(_start.AddSeconds(5));
        _sharedL2.Written = () => _clock.Now.Returns(_start.AddSeconds(11));

        var refreshed = await ReadAsync();
        refreshed.Permissions.Should().BeEmpty();
        refreshed.VerifiedAssumption.Should().Be(initial.VerifiedAssumption);
    }

    [Fact]
    public async Task GetOrCreateAsync_HomeTenantVersionChanges_RefreshesAssumedReport()
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        var proof = new VerifiedAssumption(123, 1);
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(false, provider);
        (await ReadSessionAsync(cache, proof, false)).RequiredActions.Should().BeEmpty();
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([SessionAction(proof)]);

        _versionStore.TenantVersions[1] = Guid.NewGuid();
        (await ReadSessionAsync(cache, proof, false)).RequiredActions.Single().Key.Should().Be("home-1");
    }

    [Fact]
    public async Task ReadAsync_ReplacementCacheReturnsOldStanding_RefusesActorButPreservesOrdinaryAccess()
    {
        var old = CreateData(default) with { VerifiedAssumption = new(123, 1) };
        var cache = Substitute.For<IAuthorizationCache>();
        cache.GetOrCreateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Func<CancellationToken, Task<Result<AuthorizationData>>>>(),
            Arg.Any<CancellationToken>()).Returns(old);
        var strategy = Substitute.For<IAuthorizationStrategy>();
        var principal = CreatePrincipal();
        var ct = TestContext.Current.CancellationToken;
        var member = await cache.ReadAsync(principal, strategy, ct);
        member.VerifiedAssumption.Should().BeNull();
        member.RequiredActionsUnavailable.Should().BeFalse();
        principal.Identities.Single().AddClaim(new(ClaimNames.Actor, "123"));
        var assumed = await cache.ReadAsync(principal, strategy, ct);
        assumed.VerifiedAssumption.Should().BeNull();
        assumed.RequiredActionsUnavailable.Should().BeTrue();
        assumed.Permissions.Should().BeEquivalentTo(old.Permissions);
    }

    [Fact]
    public async Task GetOrCreateAsync_PublicFactoryReceivesCopiedSnapshot_CannotTransferItsProof()
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(call => [SessionAction(call.Arg<RequiredActionSubject>().VerifiedAssumption)]);
        var cache = CreateInstance(false, provider);
        var verified = await ReadSessionAsync(cache, new(123, 1), false);
        var publicCache = (IAuthorizationCache)cache;
        var copied = await publicCache.GetOrCreateAsync("456", 42, null,
            _ => Task.FromResult(Result.Success(verified with { UserId = "456" })), TestContext.Current.CancellationToken);
        copied.RequiredActions.Single().Key.Should().Be("member");
        copied.VerifiedAssumption.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateAsync_MembershipCheckInFlight_AssumedReportRunsIndependently()
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<RequiredAction>>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var proof = call.Arg<RequiredActionSubject>().VerifiedAssumption;
            if (proof is not null)
            {
                return Task.FromResult<IReadOnlyList<RequiredAction>>([SessionAction(proof)]);
            }
            entered.TrySetResult();
            return release.Task;
        });
        var cache = CreateInstance(false, provider);
        var member = ReadSessionAsync(cache, null, false);
        try
        {
            await entered.Task.WaitAsync(TestGuard, TestContext.Current.CancellationToken);
            (await ReadSessionAsync(cache, new(123, 1), false)).RequiredActions.Single().Key.Should().Be("home-1");
        }
        finally
        {
            release.TrySetResult([]);
            await member;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetOrCreateAsync_AssumedProviderFails_DoesNotUseOrdinaryLastGoodOrBackOffItsRefresh(bool mandatory)
    {
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.FailClosed.Returns(mandatory);
        provider.CacheFor.Returns(TimeSpan.FromSeconds(2));
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<RequiredActionSubject>().VerifiedAssumption is null
                ? Task.FromResult<IReadOnlyList<RequiredAction>>([SessionAction(null)])
                : Task.FromException<IReadOnlyList<RequiredAction>>(new InvalidOperationException()));
        var cache = CreateInstance(false, provider);
        await ReadSessionAsync(cache, null, false);
        _clock.Now.Returns(_start.AddSeconds(3));

        var failed = await ReadSessionAsync(cache, new(123, 1), false);
        var refreshed = await ReadSessionAsync(cache, null, false);
        failed.RequiredActions.Should().BeEmpty();
        failed.RequiredActionsUnavailable.Should().Be(mandatory);
        refreshed.RequiredActions.Single().Key.Should().Be("member");
        await provider.Received(3).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateAsync_ReportsDisagreeOnSessionRestriction_MarksStandingUnavailable()
    {
        var action = SessionAction(null);
        var cache = CreateInstance(false, ReportProvider(action), ReportProvider(action with { PreventResolution = true }));
        (await ReadSessionAsync(cache, null, false)).RequiredActionsUnavailable.Should().BeTrue();
    }

    private static RequiredAction SessionAction(VerifiedAssumption? proof) => new()
    {
        Key = proof is null ? "member" : $"home-{proof.HomeTenantId}",
        ResolvePath = "/profile",
        Scope = RequiredActionScope.User,
        Severity = RequiredActionSeverity.Blocking,
        ResolverRoles = ["Creator"],
        PreventResolution = proof is not null,
        BlockNonResolvers = true
    };

    private static Task<AuthorizationData> ReadSessionAsync(AuthorizationCache cache, VerifiedAssumption? proof, bool byUser)
    {
        var identity = new ClaimsIdentity([new(ClaimNames.UserId, "123"), new(ClaimNames.TenantId, "42")], "test");
        if (proof is not null)
        {
            identity.AddClaim(new(ClaimNames.Actor, proof.ActorUserId.ToString()));
        }
        var principal = new ClaimsPrincipal(identity);
        Task<Result<AuthorizationData>> Build(CancellationToken _) =>
            Task.FromResult(Result.Success(CreateData(default) with { VerifiedAssumption = proof }));
        var ct = TestContext.Current.CancellationToken;
        return byUser ? cache.GetOrCreateAsync("123", 42, principal, Build, ct) : cache.GetOrCreateAsync(principal, Build, ct);
    }
}
