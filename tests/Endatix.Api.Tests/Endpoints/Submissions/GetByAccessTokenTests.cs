using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.GetByAccessToken;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class GetByAccessTokenTests
{
    private readonly IMediator _mediator;
    private readonly GetByAccessToken _endpoint;

    public GetByAccessTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetByAccessToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ValidToken_ReturnsOkWithSubmission()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature"
        };

        var submission = new Submission(SampleData.TENANT_ID, "{\"field\":\"value\"}", 123L, 1L) { Id = 456 };
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<SubmissionDetailsModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(submission.Id.ToString());
        okResult!.Value!.JsonData.Should().Be(submission.JsonData);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "invalid.token"
        };

        var result = Result<Submission>.Invalid(new ValidationError { ErrorMessage = "Invalid token" });

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature"
        };

        var result = Result<Submission>.NotFound("Submission not found");

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_TokenWithoutViewPermission_ReturnsForbidden()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature"
        };

        var result = Result<Submission>.Forbidden("Token does not have view permission");

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature"
        };

        var submission = new Submission(SampleData.TENANT_ID, "{}", 123L, 1L);
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetByAccessTokenQuery>(query =>
                query.FormId == request.FormId &&
                query.Token == request.Token),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature"
        };

        var result = Result<Submission>.Invalid(new ValidationError { ErrorMessage = "Token expired" });

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCorrectSubmissionData()
    {
        // Arrange
        var request = new GetByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature"
        };

        var jsonData = "{\"name\":\"John\",\"email\":\"john@example.com\"}";
        var submission = new Submission(SampleData.TENANT_ID, jsonData, 123L, 1L, true, 10) { Id = 456 };
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<GetByAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<SubmissionDetailsModel>;
        okResult.Should().NotBeNull();
        okResult!.Value!.JsonData.Should().Be(jsonData);
        okResult!.Value!.IsComplete.Should().BeTrue();
        okResult!.Value!.CurrentPage.Should().Be(10);
    }
}
