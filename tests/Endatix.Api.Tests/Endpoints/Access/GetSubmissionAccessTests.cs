using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetSubmissionAccessTests
{
    private readonly IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext> _accessStrategy;
    private readonly GetSubmissionAccess _endpoint;

    public GetSubmissionAccessTests()
    {
        _accessStrategy = Substitute.For<IResourceAccessQuery<SubmissionAccessData, SubmissionAccessContext>>();
        _endpoint = Factory.Create<GetSubmissionAccess>(_accessStrategy);
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

        var accessData = new SubmissionAccessData
        {
            FormId = request.FormId.ToString(),
            SubmissionId = request.SubmissionId.ToString(),
            FormPermissions = new HashSet<string> { "forms:edit" },
            SubmissionPermissions = new HashSet<string> { "submissions:edit" }
        };

        var cached = new Cached<SubmissionAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessStrategy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<SubmissionAccessData>>.Success(cached));

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
    }

    [Fact]
    public async Task ExecuteAsync_WithForbiddenAccess_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetSubmissionAccessRequest
        {
            FormId = 123,
            SubmissionId = 321
        };

        var errorResult = Result<Cached<SubmissionAccessData>>.Invalid(new ValidationError("Forbidden"));
        _accessStrategy
            .GetAccessData(Arg.Any<SubmissionAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult.ProblemDetails.Detail.Should().Contain("Forbidden");
    }
}

