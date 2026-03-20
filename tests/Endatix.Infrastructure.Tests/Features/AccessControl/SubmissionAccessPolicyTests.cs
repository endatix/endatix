using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

public sealed class SubmissionAccessPolicyTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly HybridCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SubmissionAccessPolicy _policy;

    public SubmissionAccessPolicyTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _cache = Substitute.For<HybridCache>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();

        _policy = new SubmissionAccessPolicy(_authorizationService, _cache, _dateTimeProvider);
        SetupCacheMiss();
    }

    private void SetupCacheMiss()
    {
        _cache
            .GetOrCreateAsync<object, Cached<SubmissionAccessData>>(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<Cached<SubmissionAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<object, CancellationToken, ValueTask<Cached<SubmissionAccessData>>>>();
                var ct = callInfo.Arg<CancellationToken>();
                var state = callInfo.Arg<object>();
                return factory(state, ct);
            });

        _cache
            .SetAsync(
                Arg.Any<string>(),
                Arg.Any<Cached<SubmissionAccessData>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
    }

    [Fact]
    public async Task GetAccessDataAsync_AdminUser_ReturnsEditAccess_AndCachesWithAuthTtlAndKey()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var tenantId = 1L;
        var userId = "admin";

        var utcNow = DateTime.UtcNow;
        _dateTimeProvider.Now.Returns(new DateTimeOffset(utcNow, TimeSpan.Zero));

        var expiresAt = utcNow.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId,
            roles: [SystemRole.Admin.Name],
            permissions: [Actions.Access.Hub],
            cachedAt: utcNow,
            expiresAt: expiresAt);

        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(authData));

        string? capturedCacheKey = null;
        HybridCacheEntryOptions? capturedOptions = null;

        _cache
            .SetAsync(
                Arg.Do<string>(k => capturedCacheKey = k),
                Arg.Any<Cached<SubmissionAccessData>>(),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var expectedTtl = authData.ComputeAuthTtl(utcNow);
        var expectedExpiresAt = utcNow.Add(expectedTtl);
        var expectedCacheKey = $"auth:sb_mgmt:form:{formId}:sub:{submissionId}:user:{userId}";

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.EditSubmission);
        result.Value.ExpiresAt.Should().Be(expectedExpiresAt);

        capturedCacheKey.Should().Be(expectedCacheKey);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(expectedTtl);
    }

    [Fact]
    public async Task GetAccessDataAsync_NonAdminUser_WithViewPermission_ReturnsViewAccess_AndCachesWithAuthTtlAndKey()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var tenantId = 1L;
        var userId = "user-view";

        var utcNow = DateTime.UtcNow;
        _dateTimeProvider.Now.Returns(new DateTimeOffset(utcNow, TimeSpan.Zero));

        var expiresAt = utcNow.AddMinutes(15);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Submissions.View],
            cachedAt: utcNow,
            expiresAt: expiresAt);

        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(authData));

        var expectedTtl = authData.ComputeAuthTtl(utcNow);
        var expectedCacheKey = $"auth:sb_mgmt:form:{formId}:sub:{submissionId}:user:{userId}";

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.ViewOnly);
        result.Value.ExpiresAt.Should().Be(utcNow.Add(expectedTtl));

        await _cache.Received(1).SetAsync(
            expectedCacheKey,
            Arg.Any<Cached<SubmissionAccessData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_NonAdminUser_WithEditPermission_ReturnsEditAccess_AndCachesWithAuthTtlAndKey()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var tenantId = 1L;
        var userId = "user-edit";

        var utcNow = DateTime.UtcNow;
        _dateTimeProvider.Now.Returns(new DateTimeOffset(utcNow, TimeSpan.Zero));

        var expiresAt = utcNow.AddMinutes(5);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Submissions.Edit],
            cachedAt: utcNow,
            expiresAt: expiresAt);

        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(authData));

        var expectedTtl = authData.ComputeAuthTtl(utcNow);
        var expectedCacheKey = $"auth:sb_mgmt:form:{formId}:sub:{submissionId}:user:{userId}";

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.EditSubmission);
        result.Value.ExpiresAt.Should().Be(utcNow.Add(expectedTtl));

        await _cache.Received(1).SetAsync(
            expectedCacheKey,
            Arg.Any<Cached<SubmissionAccessData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_ForbiddenWhenMissingHubPermission_ReturnsForbidden_AndDoesNotCache()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var tenantId = 1L;
        var userId = "user-no-hub";

        var utcNow = DateTime.UtcNow;
        _dateTimeProvider.Now.Returns(new DateTimeOffset(utcNow, TimeSpan.Zero));

        var expiresAt = utcNow.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId,
            roles: [],
            permissions: [Actions.Submissions.View],
            cachedAt: utcNow,
            expiresAt: expiresAt);

        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("You are not authorized to access this submission.");

        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<Cached<SubmissionAccessData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_AuthorizationServiceFails_ReturnsError_AndDoesNotCache()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Error("Authorization service unavailable"));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Authorization service unavailable");

        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<Cached<SubmissionAccessData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_ForbiddenForAnonymousUser_ReturnsForbidden_AndDoesNotCache()
    {
        // Arrange
        var formId = 10L;
        var submissionId = 20L;
        var tenantId = 1L;

        var utcNow = DateTime.UtcNow;
        _dateTimeProvider.Now.Returns(new DateTimeOffset(utcNow, TimeSpan.Zero));

        var authData = AuthorizationData.ForAnonymousUser(tenantId);
        var context = new SubmissionAccessContext(formId, submissionId);

        _authorizationService
            .GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("You are not authorized to access this submission.");

        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<Cached<SubmissionAccessData>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }
}

