using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Submissions;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public static class PermissionSets
{
    public static class Form
    {
        public static readonly string[] None = [];
        public static readonly string[] ViewOnly = [ResourcePermissions.Form.View];
        public static readonly string[] ViewAndDesign = [ResourcePermissions.Form.View, ResourcePermissions.Form.Edit];
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

public partial class SubmissionAccessControlTests
{
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly IRepository<Form> _formRepository;
    private readonly ISubmissionTokenService _tokenService;
    private readonly SubmissionAccessControl _handler;

    public SubmissionAccessControlTests()
    {
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _formRepository = Substitute.For<IRepository<Form>>();
        _tokenService = Substitute.For<ISubmissionTokenService>();
        _handler = new SubmissionAccessControl(_authorizationService, _formRepository, _tokenService);
    }

    #region Helper Methods

    private void SetupAnonymousUser()
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAnonymousUser(1)));
        _authorizationService.IsPlatformAdminAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
        _authorizationService.IsAdminAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
    }

    private void SetupAuthenticatedUser(string userId)
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser(userId, 1, [], [])));
        _authorizationService.IsPlatformAdminAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
        _authorizationService.IsAdminAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
        _authorizationService.HasPermissionAsync(Arg.Any<string>(), TestContext.Current.CancellationToken)
            .Returns(Result.Success(false));
    }

    private void SetupAdminUser()
    {
        _authorizationService.GetAuthorizationDataAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("admin", 1, [SystemRole.Admin.Name], [])));
        _authorizationService.IsPlatformAdminAsync(TestContext.Current.CancellationToken)
            .Returns(Result.Success(true));
    }

    private Task SetupFormAsync(long formId, bool isPublic)
    {
        var form = new Form(1, "test", "test@test.com") { Id = formId, IsPublic = isPublic };
        _formRepository.FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdWithRelated>(), TestContext.Current.CancellationToken)
            .Returns(form);
        return Task.CompletedTask;
    }

    private void SetupHasPermission(string action, bool hasPermission)
    {
        _authorizationService.HasPermissionAsync(action, TestContext.Current.CancellationToken)
            .Returns(Result.Success(hasPermission));
    }

    private void SetupTokenResolve(string token, long? resolvedSubmissionId)
    {
        _tokenService.ResolveTokenAsync(token, TestContext.Current.CancellationToken)
            .Returns(resolvedSubmissionId.HasValue
                ? Result.Success(resolvedSubmissionId.Value)
                : Result<long>.Error("Invalid token"));
    }

    #endregion
}

public partial class SubmissionAccessControlTests : IClassFixture<SubmissionAccessControlTests>
{
    #region Public Form Tests (Scenarios 1-4)

    [Fact]
    public async Task GetAccessDataAsync_PublicForm_NoSubmissionId_ReturnsCreateAndUploadPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new SubmissionAccessContext(formId);

        SetupAnonymousUser();
        await SetupFormAsync(formId, isPublic: true);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FormId.Should().Be(formId.ToString());
        result.Value.SubmissionId.Should().BeNull();
        result.Value.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.ViewOnly);
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.CreateAndUpload);
    }

    [Fact]
    public async Task GetAccessDataAsync_PublicForm_WithSubmissionId_NoToken_ReturnsNoSubmissionPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var context = new SubmissionAccessContext(formId, submissionId);

        SetupAnonymousUser();
        await SetupFormAsync(formId, isPublic: true);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.ViewOnly);
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.None);
    }

    #endregion

    #region Private Form Tests (Scenarios 5-8)

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_NoSubmissionId_AnonymousUser_ReturnsNoPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new SubmissionAccessContext(formId);

        SetupAnonymousUser();
        await SetupFormAsync(formId, isPublic: false);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FormPermissions.Should().BeEquivalentTo(PermissionSets.Submission.None);
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.None);
    }

    [Fact]
    public async Task GetAccessDataAsync_PrivateForm_WithSubmissionId_AuthenticatedUser_WithEditPermission_ReturnsUploadPermission()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var context = new SubmissionAccessContext(formId, submissionId);

        SetupAuthenticatedUser("user1");
        await SetupFormAsync(formId, isPublic: false);
        SetupHasPermission(Actions.Submissions.View, hasPermission: true);
        SetupHasPermission(Actions.Submissions.Edit, hasPermission: true);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.ViewAndEditAndFileOps);
    }

    #endregion

    #region Access Token Tests

    [Fact]
    public async Task GetAccessDataAsync_WithValidAccessToken_ReturnsFullPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var accessToken = "valid-token";
        var context = new SubmissionAccessContext(formId, submissionId, accessToken);

        SetupAnonymousUser();
        await SetupFormAsync(formId, isPublic: false);
        SetupTokenResolve(accessToken, submissionId);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.FullAccess);
    }

    [Fact]
    public async Task GetAccessDataAsync_WithInvalidAccessToken_ReturnsNoSubmissionPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var accessToken = "invalid-token";
        var context = new SubmissionAccessContext(formId, submissionId, accessToken);

        SetupAnonymousUser();
        await SetupFormAsync(formId, isPublic: false);
        SetupTokenResolve(accessToken, resolvedSubmissionId: 999L);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.None);
    }

    #endregion

    #region Admin Tests

    [Fact]
    public async Task GetAccessDataAsync_AdminUser_NoSubmissionId_ReturnsAllPermissions()
    {
        // Arrange
        var formId = 1L;
        var context = new SubmissionAccessContext(formId);

        SetupAdminUser();
        await SetupFormAsync(formId, isPublic: false);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.ViewAndDesign);
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.CreateAndUpload);
    }

    [Fact]
    public async Task GetAccessDataAsync_AdminUser_WithSubmissionId_ReturnsAllPermissions()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 100L;
        var context = new SubmissionAccessContext(formId, submissionId);

        SetupAdminUser();
        await SetupFormAsync(formId, isPublic: false);

        // Act
        var result = await _handler.GetAccessDataAsync(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FormPermissions.Should().BeEquivalentTo(PermissionSets.Form.ViewAndDesign);
        result.Value.SubmissionPermissions.Should().BeEquivalentTo(PermissionSets.Submission.FullAccessWithCreate);
    }

    #endregion
}
