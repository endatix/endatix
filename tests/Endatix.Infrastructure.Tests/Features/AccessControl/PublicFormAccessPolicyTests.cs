using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

internal static class TestPermissionSets
{
    public static class Form
    {
        public static readonly string[] None = [];
        public static readonly string[] ViewOnly = [ResourcePermissions.Form.View];
        public static readonly string[] ViewAndDesign = [ResourcePermissions.Form.View, ResourcePermissions.Form.Edit];
        public static readonly string[] All = [.. ResourcePermissions.Form.Sets.All];
    }

    public static class Submission
    {
        public static readonly string[] None = [];
        public static readonly string[] CreateOnly = [ResourcePermissions.Submission.Create];
        public static readonly string[] CreateAndUpload = [ResourcePermissions.Submission.Create, ResourcePermissions.Submission.UploadFile];
        public static readonly string[] ViewOnly = [ResourcePermissions.Submission.View];
        public static readonly string[] ViewAndEdit = [ResourcePermissions.Submission.View, ResourcePermissions.Submission.Edit];
        public static readonly string[] EditAndFileOps = [
            ResourcePermissions.Submission.Edit,
            ResourcePermissions.Submission.UploadFile,
            ResourcePermissions.Submission.DeleteFile
        ];
        public static readonly string[] ViewAndEditAndFileOps = [
            ResourcePermissions.Submission.View,
            ResourcePermissions.Submission.Edit,
            ResourcePermissions.Submission.UploadFile,
            ResourcePermissions.Submission.DeleteFile
        ];
        public static readonly string[] FullAccess = [
            ResourcePermissions.Submission.View,
            ResourcePermissions.Submission.Edit,
            ResourcePermissions.Submission.ViewFiles,
            ResourcePermissions.Submission.UploadFile,
            ResourcePermissions.Submission.DeleteFile
        ];
        public static readonly string[] FullAccessWithCreate = [
            ResourcePermissions.Submission.Create,
            ResourcePermissions.Submission.View,
            ResourcePermissions.Submission.Edit,
            ResourcePermissions.Submission.ViewFiles,
            ResourcePermissions.Submission.UploadFile,
            ResourcePermissions.Submission.DeleteFile
        ];
    }
}

public partial class PublicFormAccessPolicyTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly IRepository<Form> _formRepository;
    private readonly ISubmissionTokenService _tokenService;
    private readonly ISubmissionAccessTokenService _accessTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HybridCache _cache;
    private readonly PublicFormAccessPolicy _policy;

    public PublicFormAccessPolicyTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _formRepository = Substitute.For<IRepository<Form>>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _accessTokenService = Substitute.For<ISubmissionAccessTokenService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _cache = Substitute.For<HybridCache>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        // HybridCacheExtensions.TryGetValueAsync<bool> uses the `GetOrCreateAsync<object, bool>(...)`
        // overload with a (state, ct) factory; mock it so form public/private routing is deterministic.
        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>(k => k.StartsWith("meta:form:is_public:")),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });

        _policy = new PublicFormAccessPolicy(_formRepository, _tokenService, _accessTokenService, _authorizationService, _dateTimeProvider, _cache);
    }

    #region Helper Methods

    private void SetupAnonymousUser()
    {
        _authorizationService.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAnonymousUser(1)));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
    }

    private void SetupAuthenticatedUser(string userId)
    {
        _authorizationService.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser(userId, 1, [], [])));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
    }

    private void SetupAdminUser()
    {
        _authorizationService.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("admin", 1, [SystemRole.Admin.Name], [])));
    }

    private void SetupTokenResolve(string token, long? resolvedSubmissionId)
    {
        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(resolvedSubmissionId.HasValue
                ? Result.Success(resolvedSubmissionId.Value)
                : Result<long>.Error("Invalid token"));
    }

    private void SetupAccessTokenValidation(string token, bool isValid, SubmissionAccessTokenClaims? claims = null)
    {
        if (isValid && claims != null)
        {
            _accessTokenService.ValidateAccessToken(token)
                .Returns(Result<SubmissionAccessTokenClaims>.Success(claims));
        }
        else
        {
            _accessTokenService.ValidateAccessToken(token)
                .Returns(Result<SubmissionAccessTokenClaims>.Unauthorized("Invalid access token"));
        }
    }

    #endregion

    #region Public Form - No Token

    [Fact]
    public async Task GetAccessDataAsync_PublicForm_NoToken_AnonymousUser_ReturnsPublicFormPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().BeNull();
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.CreateSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_PublicForm_NoToken_CachesWithDefaultTtl_AndAnonymousInCacheKey()
    {
        // Arrange
        var formId = 1L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        HybridCacheEntryOptions? capturedOptions = null;
        string? capturedCacheKey = null;

        _cache
            .SetAsync(
                Arg.Do<string>(k => capturedCacheKey = k),
                Arg.Any<Cached<PublicFormAccessData>>(),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        capturedCacheKey.Should().Be($"ac:form:public:{formId}");
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(10));
    }

    #endregion

    #region Private Form - No Token

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoToken_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var formId = 1L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        SetupAnonymousUser();

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
        result.Errors.Should().Contain("You must be authenticated to access this form");
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoToken_UserWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var formId = 1L;
        var userId = "user-no-perms";
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        var authData = AuthorizationData.ForAuthenticatedUser(userId, 1, [], []);
        // For private forms we only allow if auth data contains `Actions.Access.PrivateForms`
        // so remove it to simulate "no permission".
        authData.Permissions.Remove(Actions.Access.PrivateForms);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        _authorizationService.HasPermissionAsync(Actions.Access.PrivateForms, Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsForbidden().Should().BeTrue();
        result.Errors.Should().Contain("You are not allowed to access this form");
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoToken_UserWithPrivateFormsPermission_ReturnsSuccess()
    {
        // Arrange
        var formId = 1L;
        var userId = "user-with-perms";
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser(userId, 1, [], [])));

        _authorizationService.HasPermissionAsync(Actions.Access.PrivateForms, Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.CreateSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoToken_AdminUser_ReturnsSuccess()
    {
        // Arrange
        var formId = 1L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        SetupAdminUser();

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoToken_CachesWithAuthTtl_AndUserIdInCacheKey()
    {
        // Arrange
        var formId = 1L;
        var userId = "user-auth";
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(15);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId, tenantId: 1, roles: [], permissions: [Actions.Access.PrivateForms],
            cachedAt: now, expiresAt: expiresAt);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        HybridCacheEntryOptions? capturedOptions = null;
        string? capturedCacheKey = null;

        _cache
            .SetAsync(
                Arg.Do<string>(k => capturedCacheKey = k),
                Arg.Any<Cached<PublicFormAccessData>>(),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        capturedCacheKey.Should().Be($"ac:form:{formId}:user:{userId}");
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    #endregion

    #region Submission Token - Private Form

    [Fact]
    public async Task GetAccessDataAsync_SubmissionToken_PrivateForm_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var formId = 1L;
        var token = "submission-token";
        var submissionId = 123L;
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        SetupAnonymousUser();
        SetupTokenResolve(token, submissionId);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessDataAsync_SubmissionToken_PrivateForm_AuthenticatedUserWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var formId = 1L;
        var token = "submission-token";
        var submissionId = 123L;
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        var authData = AuthorizationData.ForAuthenticatedUser("user", 1, [], []);
        // Simulate "authenticated user without private forms permission"
        authData.Permissions.Remove(Actions.Access.PrivateForms);

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        _authorizationService.HasPermissionAsync(Actions.Access.PrivateForms, Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));

        // Token resolution happens before the permission check in the policy, so ensure it succeeds
        // to exercise the authorization behavior being tested.
        SetupTokenResolve(token, submissionId);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsForbidden().Should().BeTrue();
    }

    #endregion

    #region Submission Token

    [Fact]
    public async Task GetAccessDataAsync_SubmissionToken_PrivateForm_CachesWithAuthTtl_AndUserInCacheKey()
    {
        // Arrange
        var formId = 1L;
        var token = "submission-token";
        var submissionId = 100L;
        var userId = "user-1";

        var now = _dateTimeProvider.Now.UtcDateTime;
        var expiresAt = now.AddMinutes(30);
        var authData = AuthorizationData.ForAuthenticatedUser(
            userId,
            tenantId: 1,
            roles: [],
            permissions: [],
            cachedAt: now,
            expiresAt: expiresAt);

        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(authData));

        SetupTokenResolve(token, submissionId);

        HybridCacheEntryOptions? capturedOptions = null;
        string? capturedCacheKey = null;

        _cache
            .SetAsync(
                Arg.Do<string>(k => capturedCacheKey = k),
                Arg.Any<Cached<PublicFormAccessData>>(),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        capturedCacheKey.Should().Be($"ac:sub_token:{token}:userId:{userId}");
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task GetAccessDataAsync_SubmissionToken_PublicForm_CachesWithDefaultTtl_AndAnonymousInCacheKey()
    {
        // Arrange
        var formId = 1L;
        var token = "submission-token";
        var submissionId = 100L;

        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        SetupAnonymousUser();
        SetupTokenResolve(token, submissionId);

        HybridCacheEntryOptions? capturedOptions = null;
        string? capturedCacheKey = null;

        _cache
            .SetAsync(
                Arg.Do<string>(k => capturedCacheKey = k),
                Arg.Any<Cached<PublicFormAccessData>>(),
                Arg.Do<HybridCacheEntryOptions?>(o => capturedOptions = o),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        capturedCacheKey.Should().Be($"ac:sub_token:{token}:userId:{AuthorizationData.ANONYMOUS_USER_ID}");
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidSubmissionToken_ReturnsReviewPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-submission-token";
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        SetupAnonymousUser();
        SetupTokenResolve(token, submissionId);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().Be(submissionId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.FillInSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_InvalidSubmissionToken_ReturnsError()
    {
        // Arrange
        var formId = 1L;
        var token = "invalid-submission-token";
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        SetupAnonymousUser();
        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result<long>.Unauthorized("Invalid or expired submission token"));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
    }

    #endregion

    #region Access Token - JWT

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithViewPermission_ReturnsViewPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.View.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().Be(submissionId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.View);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.ViewFiles);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithEditPermission_ReturnsEditPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.Edit.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.EditSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithExportPermission_ReturnsExportPermission()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.Export.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.Export);
    }

    [Fact]
    public async Task GetAccessDataAsync_InvalidAccessToken_ReturnsError()
    {
        // Arrange
        var formId = 1L;
        var token = "invalid-jwt-token";
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, false);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized().Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessDataAsync_ExpiredAccessToken_ReturnsSuccessWithMinimalTtl()
    {
        // Arrange
        var formId = 1L;
        var token = "expired-jwt-token";
        var claims = new SubmissionAccessTokenClaims(1L, [SubmissionAccessTokenPermissions.View.Name], DateTime.UtcNow.AddSeconds(-10));
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.View);
    }

    #endregion

    #region Unknown Token Type

    [Fact]
    public async Task GetAccessDataAsync_UnknownTokenType_ReturnsError()
    {
        // Arrange
        var formId = 1L;
        var token = "some-token";
        var context = new PublicFormAccessContext(formId, token, (SubmissionTokenType)999);

        SetupAnonymousUser();

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Unknown token type");
    }

    #endregion

    #region Access Token - Combined Permissions

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithViewAndEditPermissions_ReturnsCombinedPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "multi-perm-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.View.Name, SubmissionAccessTokenPermissions.Edit.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.EditSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithAllPermissions_ReturnsAllPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "all-perms-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [
                SubmissionAccessTokenPermissions.View.Name,
                SubmissionAccessTokenPermissions.Edit.Name,
                SubmissionAccessTokenPermissions.Export.Name
            ],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.View);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.Edit);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.Export);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithEmptyPermissions_ReturnsViewFormOnly()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var token = "empty-perms-token";
        var claims = new SubmissionAccessTokenClaims(submissionId, [], DateTime.UtcNow.AddHours(1));
        var context = new PublicFormAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().Be(submissionId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        result.Value.Data.SubmissionPermissions.Should().BeEmpty();
    }

    #endregion

    #region Error Paths

    [Fact]
    public async Task GetAccessDataAsync_FormNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var formId = 999L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<object, CancellationToken, ValueTask<bool>>>();
                var ct = callInfo.Arg<CancellationToken>();
                return factory(null!, ct);
            });

        _formRepository
            .FirstOrDefaultAsync(Arg.Any<ISpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found");
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_AuthorizationServiceFails_ReturnsError()
    {
        // Arrange
        var formId = 1L;
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(false));

        _authorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Error("Authorization service unavailable"));

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Authorization service unavailable");
    }

    #endregion

    #region Cache Behavior

    [Fact]
    public async Task GetAccessDataAsync_CacheHit_ReturnsCachedData_WithoutInvokingFactory()
    {
        // Arrange
        var formId = 1L;
        var cachedData = new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = null,
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.CreateSubmission],
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        var cachedEnvelope = Cached<PublicFormAccessData>.Create(cachedData, _dateTimeProvider.Now.UtcDateTime, TimeSpan.FromMinutes(10));
        var context = new PublicFormAccessContext(formId);

        _cache
            .GetOrCreateAsync<object, bool>(
                Arg.Is<string>($"meta:form:is_public:{formId}"),
                Arg.Any<object>(),
                Arg.Any<Func<object, CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<bool>(true));

        _cache
            .GetOrCreateAsync(
                Arg.Is<string>(k => k.StartsWith("ac:form:")),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Cached<PublicFormAccessData>>(cachedEnvelope));

        var factoryInvoked = false;

        _cache
            .GetOrCreateAsync(
                Arg.Is<string>(k => k.StartsWith("ac:form:")),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<Cached<PublicFormAccessData>>>>();
                var ct = callInfo.Arg<CancellationToken>();
                factoryInvoked = true;
                return factory(ct);
            });

        // Act
        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        factoryInvoked.Should().BeFalse();
    }

    #endregion
}
