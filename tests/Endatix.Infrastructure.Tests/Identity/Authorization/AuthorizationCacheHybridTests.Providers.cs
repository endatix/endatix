using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed partial class AuthorizationCacheHybridTests
{
    [Theory]
    [InlineData(true, "", "/profile")]
    [InlineData(false, "profile", "profile")]
    public async Task GetOrCreateAsync_InvalidReport_UsesProviderFailurePolicy(bool mandatory, string key, string path)
    {
        // Arrange
        var provider = ReportProvider(new RequiredAction
        {
            Key = key,
            ResolvePath = path,
            Scope = RequiredActionScope.User,
            Severity = RequiredActionSeverity.Notice
        });
        provider.FailClosed.Returns(mandatory);
        var original = CreateData(default);

        // Act
        var result = await CreateInstance(false, provider).GetOrCreateAsync(CreatePrincipal(),
            _ => Task.FromResult(Result.Success(original)), TestContext.Current.CancellationToken);

        // Assert
        result.RequiredActionsUnavailable.Should().Be(mandatory);
        result.RequiredActions.Should().BeEmpty();
        result.Permissions.Should().BeEquivalentTo(original.Permissions);
    }

    [Theory]
    [InlineData(false, "roles")]
    [InlineData(false, "scope")]
    [InlineData(true, "severity")]
    public async Task GetOrCreateAsync_IncompleteReport_AppliesOnlyTheFaultyProvidersPolicy(bool mandatory, string field)
    {
        // Arrange
        var notice = new RequiredAction
        {
            Key = "help",
            ResolvePath = "/help",
            Scope = RequiredActionScope.User,
            Severity = RequiredActionSeverity.Notice
        };
        var invalid = field switch
        {
            "roles" => notice with { ResolverRoles = null! },
            "scope" => notice with { Scope = (RequiredActionScope)99 },
            _ => notice with { Severity = (RequiredActionSeverity)99 }
        };
        var malformed = ReportProvider(invalid with { Key = "incomplete" });
        malformed.FailClosed.Returns(mandatory);
        var cache = CreateInstance(false, ReportProvider(notice), malformed);

        // Act
        var snapshot = await cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))),
            TestContext.Current.CancellationToken);

        // Assert
        snapshot.RequiredActionsUnavailable.Should().Be(mandatory);
        snapshot.RequiredActions.Select(action => action.Key).Should().Equal(mandatory ? [] : new[] { "help" });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GetOrCreateAsync_OptionalFallbackExpiresDuringWrite_DropsOnlyItsActions(bool hasMandatory, bool conflict)
    {
        // Arrange
        var optional = NoticeProvider("Creator", false);
        optional.CacheFor.Returns(TimeSpan.FromSeconds(5));
        var mandatory = Substitute.For<IRequiredActionProvider>();
        mandatory.CacheFor.Returns(TimeSpan.FromSeconds(30));
        mandatory.FailClosed.Returns(true);
        mandatory.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(true, hasMandatory ? [mandatory, optional] : [optional]);
        (await SnapshotAsync(cache)).RequiredActions.Should().ContainSingle();
        optional.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<RequiredAction>>(new HttpRequestException()));
        if (conflict)
        {
            var report = new RequiredAction { Key = "profile.complete", ResolvePath = "/other", Scope = RequiredActionScope.Tenant, Severity = RequiredActionSeverity.Blocking };
            mandatory.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([report]);
        }
        _clock.Now.Returns(_start.AddSeconds(55));
        _sharedL2.Written = () => _clock.Now.Returns(_start.AddSeconds(61));

        // Act
        var refreshed = await SnapshotAsync(cache);

        // Assert
        refreshed.RequiredActions.Select(a => a.ResolvePath).Should().Equal(conflict ? ["/other"] : Array.Empty<string>());
        refreshed.RequiredActionsUnavailable.Should().BeFalse();
        refreshed.ExpiresAt.Should().Be(_start.AddSeconds(70).UtcDateTime);
    }

    [Fact]
    public async Task GetOrCreateAsync_PermissionExpiresWhileWriting_UsesFreshGrants()
    {
        // Arrange
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.FailClosed.Returns(true);
        provider.CacheFor.Returns(TimeSpan.FromSeconds(5));
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(true, provider);
        var expiry = _start.AddSeconds(10).UtcDateTime;
        var allowed = CreateData(expiry);
        var revoked = allowed with { Permissions = [], ExpiresAt = default };
        Task<AuthorizationData> ReadAsync() => cache.GetOrCreateAsync(CreatePrincipal(),
            _ => Task.FromResult(Result.Success(_clock.Now.UtcDateTime < expiry ? allowed : revoked)), TestContext.Current.CancellationToken);
        (await ReadAsync()).Permissions.Should().Contain("forms.view");
        _clock.Now.Returns(_start.AddSeconds(5));
        _sharedL2.Written = () => _clock.Now.Returns(_start.AddSeconds(11));

        // Act
        var current = await ReadAsync();

        // Assert
        current.Permissions.Should().BeEmpty();
        current.RequiredActionsUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAsync_MandatoryReportExpiresDuringCacheWrite_ReturnsUnavailable()
    {
        // Arrange
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.FailClosed.Returns(true);
        provider.CacheFor.Returns(TimeSpan.FromSeconds(5));
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(true, provider);
        var first = await SnapshotAsync(cache);
        _clock.Now.Returns(_start.AddSeconds(5));
        _sharedL2.Written = () => _clock.Now.Returns(_start.AddSeconds(11));

        // Act
        var expired = await SnapshotAsync(cache);

        // Assert
        first.RequiredActionsUnavailable.Should().BeFalse();
        expired.RequiredActionsUnavailable.Should().BeTrue();
        expired.ETag.Should().NotBe(first.ETag);
    }

    [Fact]
    public async Task GetOrCreateAsync_TokenMapsToAnotherTenant_UsesTheHydratedTenantsVersion()
    {
        // Arrange
        const long localTenant = 99;
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(false, provider);
        var principal = CreatePrincipal();
        var data = CreateData(default) with { TenantId = localTenant };
        Task<AuthorizationData> ReadAsync() => cache.GetOrCreateAsync(principal,
            _ => Task.FromResult(Result.Success(data)), TestContext.Current.CancellationToken);
        var original = await ReadAsync();

        // Act
        var action = new RequiredAction { Key = "setup", ResolvePath = "/setup", Scope = RequiredActionScope.Tenant, Severity = RequiredActionSeverity.Blocking };
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([action]);
        _versionStore.TenantVersions[localTenant] = Guid.NewGuid();
        var refreshed = await ReadAsync();

        // Assert
        original.RequiredActions.Should().BeEmpty();
        refreshed.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("setup");
    }

    [Fact]
    public async Task GetOrCreateAsync_MandatoryClearanceExpiresDuringAnotherProviderRead_ReturnsUnavailable()
    {
        // Arrange
        var mandatory = Substitute.For<IRequiredActionProvider>();
        mandatory.FailClosed.Returns(true);
        mandatory.CacheFor.Returns(TimeSpan.FromMinutes(1));
        mandatory.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(new RequiredActionReport([], _start.AddSeconds(10)));
        var remote = Substitute.For<IRequiredActionProvider>();
        remote.CacheFor.Returns(TimeSpan.FromSeconds(5));
        remote.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([]);
        var cache = CreateInstance(false, mandatory, remote);
        (await SnapshotAsync(cache)).RequiredActionsUnavailable.Should().BeFalse();
        _clock.Now.Returns(_start.AddSeconds(5));
        remote.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _clock.Now.Returns(_start.AddSeconds(11));
            return Array.Empty<RequiredAction>();
        });

        // Act
        var snapshot = await SnapshotAsync(cache);

        // Assert
        snapshot.RequiredActionsUnavailable.Should().BeTrue();
        snapshot.RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_ProvidersAwaitEachOther_BothCompleteWithinTheirBudgets()
    {
        // Arrange
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = CreateInstance(false, new RendezvousProvider(first, second), new RendezvousProvider(second, first));

        // Act
        var snapshot = await SnapshotAsync(cache);

        // Assert
        snapshot.RequiredActionsUnavailable.Should().BeFalse();
        first.Task.IsCompletedSuccessfully.Should().BeTrue();
        second.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class RendezvousProvider(TaskCompletionSource ready, TaskCompletionSource other) : IRequiredActionProvider
    {
        public string CacheConfigurationKey => "paired";
        public bool FailClosed => true;

        public async Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            ready.SetResult();
            await other.Task.WaitAsync(cancellationToken);
            return [];
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOrCreateAsync_ExpiredProviderFails_UsesBoundedOptionalFallbackOnly(bool mandatory)
    {
        // Arrange
        var provider = Substitute.For<IRequiredActionProvider>();
        provider.CacheFor.Returns(TimeSpan.FromSeconds(5));
        provider.FailClosed.Returns(mandatory);
        var report = new RequiredAction { Key = "setup", ResolvePath = "/setup", Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking };
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns([report]);
        var cache = CreateInstance(false, provider);
        var good = await SnapshotAsync(cache);

        // Act
        provider.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<RequiredAction>>(new HttpRequestException()));
        _clock.Now.Returns(_start.AddSeconds(5));
        var fallback = await SnapshotAsync(cache);

        // Assert
        good.ExpiresAt.Should().Be(_start.AddSeconds(5).UtcDateTime);
        fallback.RequiredActionsUnavailable.Should().Be(mandatory);
        if (mandatory)
        {
            fallback.RequiredActions.Should().BeEmpty();
        }
        else
        {
            fallback.RequiredActions.Should().ContainSingle().Which.Key.Should().Be("setup");
        }
        _clock.Now.Returns(_start.AddMinutes(1));
        (await SnapshotAsync(cache)).RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_OneProviderDoesNotFinish_OtherProviderStartsAndRemainsFreshThroughRetry()
    {
        // Arrange
        var release = new TaskCompletionSource<IReadOnlyList<RequiredAction>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = Substitute.For<IRequiredActionProvider>();
        slow.Timeout.Returns(TimeSpan.FromMilliseconds(300));
        slow.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var fast = Substitute.For<IRequiredActionProvider>();
        fast.GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            starting.TrySetResult();
            return Array.Empty<RequiredAction>();
        });
        var cache = CreateInstance(false, slow, fast);

        // Act
        try
        {
            var read = SnapshotAsync(cache);
            await starting.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            (await read.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).RequiredActionsUnavailable.Should().BeFalse();
            await SnapshotAsync(cache);
            _clock.Now.Returns(_start.AddSeconds(15));
            await SnapshotAsync(cache);
        }
        finally
        {
            release.TrySetResult([]);
        }

        // Assert
        await fast.Received(1).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
        await slow.Received(1).GetPendingAsync(Arg.Any<RequiredActionSubject>(), Arg.Any<CancellationToken>());
    }

    private static Task<AuthorizationData> SnapshotAsync(AuthorizationCache cache) =>
        cache.GetOrCreateAsync(CreatePrincipal(), _ => Task.FromResult(Result.Success(CreateData(default))), TestContext.Current.CancellationToken);
}
