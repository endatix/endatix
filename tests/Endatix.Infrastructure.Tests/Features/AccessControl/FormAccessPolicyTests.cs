using System.Collections.Immutable;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

public class FormAccessPolicyTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly HybridCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly FormAccessPolicy _policy;

    public FormAccessPolicyTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _cache = Substitute.For<HybridCache>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<Cached<FormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<object, CancellationToken, ValueTask<Cached<FormAccessData>>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(null!, ct);
            });

        _policy = new FormAccessPolicy(_authorizationService, _cache, _dateTimeProvider);
    }

    #region Authorization Failure

    [Fact]
    public async Task GetAccessDataAsync_AuthorizationServiceFails_ReturnsError()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Error("Authorization service unavailable"));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Authorization service unavailable");
    }

    [Fact]
    public async Task GetAccessDataAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        var authData = AuthorizationData.ForAnonymousUser(tenantId: 1);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
        result.Errors.Should().Contain("You are not authorized to access this form.");
    }

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUserWithoutHubPermission_ReturnsForbidden()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);
        var userId = "user-no-hub";

        var authData = AuthorizationData.ForAuthenticatedUser(userId, tenantId: 1, roles: [], permissions: []);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsForbidden().Should().BeTrue();
        result.Errors.Should().Contain("You are not authorized to access this form.");
    }

    #endregion

    #region View Access

    [Fact]
    public async Task GetAccessDataAsync_AdminUser_ReturnsEditAccessPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        var authData = AuthorizationData.ForAuthenticatedUser(
            "admin",
            tenantId: 1,
            roles: [SystemRole.Admin.Name],
            permissions: [Actions.Access.Hub]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.EditForm);
    }

    [Fact]
    public async Task GetAccessDataAsync_UserWithFormsViewPermission_ReturnsViewAccessPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);
        var userId = "user-view";

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.View]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
    }

    #endregion

    #region Edit Access

    [Fact]
    public async Task GetAccessDataAsync_UserWithFormsEditPermission_ReturnsEditAccessPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);
        var userId = "user-edit";

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.Edit]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.EditForm);
    }

    [Fact]
    public async Task GetAccessDataAsync_NonAdminUserWithEditOnlyPermission_ReturnsEditAccessPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        var authData = AuthorizationData.ForAuthenticatedUser(
            "user-edit-only",
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.Edit]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.EditForm);
    }

    #endregion

    #region Permission Priority (Edit takes precedence over View)

    [Fact]
    public async Task GetAccessDataAsync_UserWithBothViewAndEditPermissions_ReturnsEditPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);
        var userId = "user-both";

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.View, Actions.Forms.Edit]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.EditForm);
    }

    #endregion

    #region Caching

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUser_CachesWithCorrectKey()
    {
        // Arrange
        var formId = 42L;
        var userId = "user-cache";
        var context = new FormAccessContext(formId);

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.View],
            cachedAt: now,
            expiresAt: expiresAt);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        await _cache.Received(1).GetOrCreateAsync(
            Arg.Is<string>(k => k == $"auth:form_mgmt:{formId}:user:{userId}"),
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<Cached<FormAccessData>>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_CacheHit_ReturnsCachedData()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        var authData = AuthorizationData.ForAuthenticatedUser(
            "user",
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.View]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        var cachedData = new FormAccessData
        {
            FormId = formId.ToString(),
            Permissions = ResourcePermissions.Form.Sets.ViewForm.ToImmutableHashSet()
        };
        var cachedEnvelope = Cached<FormAccessData>.Create(cachedData, _dateTimeProvider.Now.UtcDateTime, TimeSpan.FromMinutes(10));

        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<ICachedData<FormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ICachedData<FormAccessData>>(cachedEnvelope));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
    }

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUser_CachesWithAuthTtl()
    {
        // Arrange
        var formId = 1L;
        var context = new FormAccessContext(formId);

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            "user",
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Forms.View],
            cachedAt: now,
            expiresAt: expiresAt);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        await _cache.Received(1).GetOrCreateAsync(
            Arg.Is<string>(k => k.StartsWith($"auth:form_mgmt:{formId}:")),
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<Cached<FormAccessData>>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
