using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Ardalis.Specification;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using ResourcePermissions = Endatix.Core.Authorization.Permissions.ResourcePermissions;

namespace Endatix.Infrastructure.Tests.Features.AccessPolicy;

public static class PermissionSets
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

public partial class SubmissionAccessPolicyTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly IRepository<Form> _formRepository;
    private readonly ISubmissionTokenService _tokenService;
    private readonly ISubmissionAccessTokenService _accessTokenService;
    private readonly HybridCache _cache;
    private readonly SubmissionAccessPolicy _policy;

    public SubmissionAccessPolicyTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _formRepository = Substitute.For<IRepository<Form>>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _accessTokenService = Substitute.For<ISubmissionAccessTokenService>();
        _cache = Substitute.For<HybridCache>();
        
        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<bool>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<bool>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });
        
        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, ValueTask<Cached<SubmissionAccessData>>>>(),
                Arg.Any<HybridCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.Arg<Func<CancellationToken, ValueTask<Cached<SubmissionAccessData>>>>();
                var token = callInfo.Arg<CancellationToken>();
                return factory(token);
            });
        
        _policy = new SubmissionAccessPolicy(_formRepository, _tokenService, _accessTokenService, _authorizationService, _cache);
    }

    #region Helper Methods

    private void SetupAnonymousUser()
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAnonymousUser(1)));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
    }

    private void SetupAuthenticatedUser(string userId)
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser(userId, 1, [], [])));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
    }

    private void SetupAdminUser()
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("admin", 1, [SystemRole.Admin.Name], [])));
    }

    private void SetupTokenResolve(string token, long? resolvedSubmissionId)
    {
        _tokenService.ResolveTokenAsync(token, TestContext.Current.CancellationToken)
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
                .Returns(Result<SubmissionAccessTokenClaims>.Error("Invalid access token"));
        }
    }

    #endregion

    #region Submission Token

    [Fact]
    public async Task GetAccessDataAsync_ValidSubmissionToken_ReturnsReviewPermissions()
    {
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-submission-token";
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        SetupAnonymousUser();
        SetupTokenResolve(token, submissionId);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().Be(submissionId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.None);
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.ReviewSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_InvalidSubmissionToken_ReturnsError()
    {
        var formId = 1L;
        var token = "invalid-submission-token";
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.SubmissionToken);

        SetupAnonymousUser();
        _tokenService.ResolveTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(Result<long>.Error("Invalid or expired token"));

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired submission token");
    }

    #endregion

    #region Access Token - JWT

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithViewPermission_ReturnsViewPermissions()
    {
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.View.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.FormId.Should().Be(formId.ToString());
        result.Value.Data.SubmissionId.Should().Be(submissionId.ToString());
        result.Value.Data.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.None);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.View);
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.ViewFiles);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithEditPermission_ReturnsEditPermissions()
    {
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.Edit.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.EditSubmission);
    }

    [Fact]
    public async Task GetAccessDataAsync_ValidAccessToken_WithExportPermission_ReturnsExportPermission()
    {
        var formId = 1L;
        var submissionId = 100L;
        var token = "valid-jwt-token";
        var claims = new SubmissionAccessTokenClaims(
            submissionId,
            [SubmissionAccessTokenPermissions.Export.Name],
            DateTime.UtcNow.AddHours(1)
        );
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.Export);
    }

    [Fact]
    public async Task GetAccessDataAsync_InvalidAccessToken_ReturnsError()
    {
        var formId = 1L;
        var token = "invalid-jwt-token";
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, false);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid access token");
    }

    [Fact]
    public async Task GetAccessDataAsync_ExpiredAccessToken_ReturnsSuccessWithMinimalTtl()
    {
        var formId = 1L;
        var token = "expired-jwt-token";
        var claims = new SubmissionAccessTokenClaims(1L, [SubmissionAccessTokenPermissions.View.Name], DateTime.UtcNow.AddSeconds(-10));
        var context = new SubmissionAccessContext(formId, token, SubmissionTokenType.AccessToken);

        SetupAccessTokenValidation(token, true, claims);

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.SubmissionPermissions.Should().Contain(ResourcePermissions.Submission.View);
    }

    #endregion

    #region Unknown Token Type

    [Fact]
    public async Task GetAccessDataAsync_UnknownTokenType_ReturnsError()
    {
        var formId = 1L;
        var token = "some-token";
        var context = new SubmissionAccessContext(formId, token, (SubmissionTokenType)999);

        SetupAnonymousUser();

        var result = await _policy.GetAccessData(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Unknown token type");
    }

    #endregion
}
