using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormPublicAccessTests
{
    private readonly IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> _accessPolicy;
    private readonly GetFormPublicAccess _endpoint;

    public GetFormPublicAccessTests()
    {
        _accessPolicy = Substitute.For<IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>>();
        _endpoint = Factory.Create<GetFormPublicAccess>(_accessPolicy);
    }

    [Fact]
    public void Endpoint_CanBeCreated()
        => _endpoint.Should().NotBeNull();

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAccess_ReturnsOkResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 123 };

        var accessData = new PublicFormAccessData
        {
            FormId = "123",
            SubmissionId = null,
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.CreateSubmission]
        };
        var cached = new Cached<PublicFormAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<PublicFormAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormPublicAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(accessData.FormId);
        okResult.Value!.SubmissionId.Should().BeNull();
        okResult.Value!.FormPermissions.Should().BeEquivalentTo(accessData.FormPermissions);
        okResult.Value!.SubmissionPermissions.Should().BeEquivalentTo(accessData.SubmissionPermissions);
        okResult.Value!.ETag.Should().Be(cached.ETag);
        okResult.Value!.CachedAt.Should().Be(cached.CachedAt);
        okResult.Value!.ExpiresAt.Should().Be(cached.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithTokenAndSubmissionId_ReturnsSubmissionPermissions()
    {
        // Arrange
        var formId = 100L;
        var submissionId = 200L;
        var request = new GetFormPublicAccessRequest
        {
            FormId = formId,
            Token = "submission-token",
            TokenType = SubmissionTokenType.SubmissionToken
        };

        var accessData = new PublicFormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = submissionId.ToString(),
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = [.. ResourcePermissions.Submission.Sets.FillInSubmission]
        };
        var cached = new Cached<PublicFormAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-token");

        _accessPolicy
            .GetAccessData(Arg.Is<PublicFormAccessContext>(c => c.FormId == formId), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<PublicFormAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormPublicAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(formId.ToString());
        okResult.Value!.SubmissionId.Should().Be(submissionId.ToString());
        okResult.Value!.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.FillInSubmission);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnauthorized_ReturnsUnauthorizedProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 123 };

        var errorResult = Result<Cached<PublicFormAccessData>>.Unauthorized("You must be authenticated to access this form");
        _accessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problemResult.ProblemDetails.Detail.Should().Contain("authenticated");
    }

    [Fact]
    public async Task ExecuteAsync_WithForbidden_ReturnsForbiddenProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 123 };

        var errorResult = Result<Cached<PublicFormAccessData>>.Forbidden("You are not allowed to access this form");
        _accessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problemResult.ProblemDetails.Detail.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_WithNotFound_ReturnsNotFoundProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 999 };

        var errorResult = Result<Cached<PublicFormAccessData>>.NotFound("Form not found");
        _accessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problemResult.ProblemDetails.Detail.Should().Contain("Form not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnexpectedError_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest { FormId = 123 };

        var errorResult = Result<Cached<PublicFormAccessData>>.Error("unexpected error");
        _accessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problemResult.ProblemDetails.Detail.Should().Contain("unexpected error");
    }
}
