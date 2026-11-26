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

        var member = await ReadSessionAsync(writer, null, byUser);
        var firstHome = await ReadSessionAsync(writer, new(123, 1), byUser);
        var secondHome = await ReadSessionAsync(writer, new(123, 2), byUser);
        var distributed = await ReadSessionAsync(reader, new(123, 2), byUser);

        member.RequiredActions.Single().Key.Should().Be("member");
        firstHome.RequiredActions.Single().Key.Should().Be("home-1");
        secondHome.RequiredActions.Single().Key.Should().Be("home-2");
        distributed.VerifiedAssumption.Should().Be(new VerifiedAssumption(123, 2));
        distributed.RequiredActions.Single().CanResolve.Should().BeFalse();
        await provider.Received(3).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
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
        Key = proof is null ? "member" : $"home-{proof.HomeTenantId}", ResolvePath = "/profile", Scope = RequiredActionScope.User,
        Severity = RequiredActionSeverity.Blocking, ResolverRoles = ["Creator"], PreventResolution = proof is not null, BlockNonResolvers = true
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
