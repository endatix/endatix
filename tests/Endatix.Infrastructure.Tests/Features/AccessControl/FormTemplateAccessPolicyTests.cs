using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

public class FormTemplateAccessPolicyTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly HybridCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly FormTemplateAccessPolicy _policy;

    public FormTemplateAccessPolicyTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _cache = Substitute.For<HybridCache>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<Cached<FormTemplateAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<object, CancellationToken, ValueTask<Cached<FormTemplateAccessData>>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(null!, ct);
            });

        _policy = new FormTemplateAccessPolicy(_authorizationService, _cache, _dateTimeProvider);
    }

    #region Authorization Failure

    [Fact]
    public async Task GetAccessDataAsync_AuthorizationServiceFails_ReturnsError()
    {
        // Arrange
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);

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
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);

        var authData = AuthorizationData.ForAnonymousUser(tenantId: 1);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
        result.Errors.Should().Contain("You are not authorized to access this form template.");
    }

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUserWithoutHubPermission_ReturnsForbidden()
    {
        // Arrange
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);
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
        result.Errors.Should().Contain("You are not authorized to access this form template.");
    }

    #endregion

    #region View Access

    [Fact]
    public async Task GetAccessDataAsync_AdminUser_ReturnsEditAccessPermissions()
    {
        // Arrange
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);

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
        result.Value.Data.TemplateId.Should().Be(templateId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Template.Sets.EditTemplate);
    }

    [Fact]
    public async Task GetAccessDataAsync_UserWithTemplatesViewPermission_ReturnsViewAccessPermissions()
    {
        // Arrange
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);
        var userId = "user-view";

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Templates.View]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.TemplateId.Should().Be(templateId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Template.Sets.ViewTemplate);
    }

    #endregion

    #region Edit Access

    [Fact]
    public async Task GetAccessDataAsync_UserWithTemplatesEditPermission_ReturnsEditAccessPermissions()
    {
        // Arrange
        var templateId = 1L;
        var context = new FormTemplateAccessContext(templateId);
        var userId = "user-edit";

        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Templates.Edit]);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.TemplateId.Should().Be(templateId.ToString());
        result.Value.Data.Permissions.Should().BeEquivalentTo(ResourcePermissions.Template.Sets.EditTemplate);
    }

    #endregion

    #region Caching

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUser_CachesWithCorrectKey()
    {
        // Arrange
        var templateId = 42L;
        var userId = "user-cache";
        var context = new FormTemplateAccessContext(templateId);

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Templates.View],
            cachedAt: now,
            expiresAt: expiresAt);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        await _cache.Received(1).GetOrCreateAsync(
            Arg.Is<string>(k => k == $"auth:tpl_mgmt:{templateId}:user:{userId}"),
            Arg.Any<object>(),
            Arg.Any<Func<object, CancellationToken, ValueTask<Cached<FormTemplateAccessData>>>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessDataAsync_AuthenticatedUser_CachesWithAuthTtl()
    {
        // Arrange
        var templateId = 42L;
        var userId = "user-cache-ttl";
        var context = new FormTemplateAccessContext(templateId);

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(25);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [Actions.Access.Hub, Actions.Templates.View],
            cachedAt: now,
            expiresAt: expiresAt);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        Cached<FormTemplateAccessData>? captured = null;
        HybridCacheEntryOptions? capturedOptions = null;

        _cache
            .SetAsync(
                Arg.Any<string>(),
                Arg.Do<Cached<FormTemplateAccessData>>(c => captured = c),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var expectedTtl = authData.ComputeAuthTtl(now);

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        captured.Should().NotBeNull();
        captured!.ExpiresAt.Should().Be(now.Add(expectedTtl));
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(expectedTtl);
    }

    #endregion
}

