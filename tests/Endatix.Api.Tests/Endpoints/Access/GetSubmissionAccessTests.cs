using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetSubmissionAccessTests
{
    private readonly IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext> _accessPolicy;
    private readonly GetSubmissionAccess _endpoint;

    public GetSubmissionAccessTests()
    {
        _accessPolicy = Substitute.For<IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext>>();
        _endpoint = Factory.Create<GetSubmissionAccess>(_accessPolicy);
    }

    [Fact]
    public void Endpoint_CanBeCreated()
    {
        _endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAccess_ReturnsOkResult()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest
        {
            FormId = 123,
            SubmissionId = 321
        };

        var accessData = SubmissionAccessData.CreateWithViewAccess(123, 321);
        var cached = new Cached<SubmissionAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessPolicy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<SubmissionAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetSubmissionAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(accessData.FormId);
        okResult.Value!.SubmissionId.Should().Be(accessData.SubmissionId);
        okResult.Value!.FormPermissions.Should().BeEquivalentTo(accessData.FormPermissions);
        okResult.Value!.SubmissionPermissions.Should().BeEquivalentTo(accessData.SubmissionPermissions);
        okResult.Value!.ETag.Should().Be(cached.ETag);
        okResult.Value!.CachedAt.Should().Be(cached.CachedAt);
        okResult.Value!.ExpiresAt.Should().Be(cached.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnauthorized_ReturnsUnauthorizedProblemResult()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest
        {
            FormId = 123,
            SubmissionId = 321
        };

        var errorResult = Result<ICachedData<SubmissionAccessData>>.Unauthorized("You are not authorized to access this submission.");
        _accessPolicy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problemResult.ProblemDetails.Detail.Should().Contain("not authorized");
    }

    [Fact]
    public async Task ExecuteAsync_WithForbidden_ReturnsForbiddenProblemResult()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest
        {
            FormId = 123,
            SubmissionId = 321
        };

        var errorResult = Result<ICachedData<SubmissionAccessData>>.Forbidden("You are not authorized to access this submission.");
        _accessPolicy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problemResult.ProblemDetails.Detail.Should().Contain("not authorized");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnexpectedError_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest
        {
            FormId = 123,
            SubmissionId = 321
        };

        var errorResult = Result<ICachedData<SubmissionAccessData>>.Error("unexpected error");
        _accessPolicy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problemResult.ProblemDetails.Detail.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task ExecuteAsync_WithViewAccess_ReturnsViewPermissions()
    {
        // Arrange
        var formId = 100L;
        var submissionId = 200L;
        var request = new GetSubmissionAccessRequest { FormId = formId, SubmissionId = submissionId };

        var accessData = SubmissionAccessData.CreateWithViewAccess(formId, submissionId);
        var cached = new Cached<SubmissionAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-view");

        _accessPolicy
            .GetAccessData(Arg.Is<SubmissionAccessContext>(c => c.FormId == formId && c.SubmissionId == submissionId), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<SubmissionAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetSubmissionAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(formId.ToString());
        okResult.Value!.SubmissionId.Should().Be(submissionId.ToString());
        okResult.Value!.FormPermissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
        okResult.Value!.SubmissionPermissions.Should().BeEquivalentTo(ResourcePermissions.Submission.Sets.ViewOnly);
    }
}

