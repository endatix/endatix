using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Caching;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormAccessTests
{
    private readonly IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext> _accessStrategy;
    private readonly GetFormPublicAccess _endpoint;

    public GetFormAccessTests()
    {
        _accessStrategy = Substitute.For<IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext>>();
        _endpoint = Factory.Create<GetFormPublicAccess>(_accessStrategy);
    }

    [Fact]
    public void Endpoint_CanBeCreated()
    {
        _endpoint.Should().NotBeNull();
    }

    [Fact]
    public void Endpoint_AcceptsAccessStrategy()
    {
        _endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAccess_ReturnsOkResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 123,
            Token = "token",
            TokenType = SubmissionTokenType.AccessToken
        };

        var accessData = new SubmissionAccessData
        {
            FormId = request.FormId.ToString(),
            SubmissionId = "sub-1",
            FormPermissions = new HashSet<string> { "forms:view" },
            SubmissionPermissions = new HashSet<string> { "submissions:view" }
        };

        var cached = new Cached<SubmissionAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessStrategy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<SubmissionAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormPublicAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(accessData.FormId);
        okResult.Value!.SubmissionId.Should().Be(accessData.SubmissionId);
        okResult.Value!.FormPermissions.Should().BeEquivalentTo(accessData.FormPermissions);
        okResult.Value!.SubmissionPermissions.Should().BeEquivalentTo(accessData.SubmissionPermissions);
        okResult.Value!.ETag.Should().Be(cached.ETag);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedAccess_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 123,
            Token = "token",
            TokenType = SubmissionTokenType.AccessToken
        };

        var errorResult = Result<Cached<SubmissionAccessData>>.Invalid(new ValidationError("access denied"));
        _accessStrategy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult.ProblemDetails.Detail.Should().Contain("access denied");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnexpectedError_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormPublicAccessRequest
        {
            FormId = 123,
            Token = "token",
            TokenType = SubmissionTokenType.AccessToken
        };

        var errorResult = Result<Cached<SubmissionAccessData>>.Error("unexpected error");
        _accessStrategy
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
}

