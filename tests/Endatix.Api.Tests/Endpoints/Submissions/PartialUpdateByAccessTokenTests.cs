using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateByAccessTokenTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdateByAccessToken _endpoint;

    public PartialUpdateByAccessTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdateByAccessToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedSubmission()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            IsComplete = true,
            CurrentPage = 5,
            JsonData = "{\"field\":\"newValue\"}",
            Metadata = "{\"meta\":\"data\"}"
        };

        var updatedSubmission = Submission.Create(456, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 123L,
            FormDefinitionId: 1L,
            JsonData: request.JsonData!,
            IsComplete: true,
            CurrentPage: 5));
        var result = Result.Success(updatedSubmission);

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(updatedSubmission.Id.ToString());
        okResult!.Value!.JsonData.Should().Be(request.JsonData);
        okResult!.Value!.IsComplete.Should().BeTrue();
        okResult!.Value!.CurrentPage.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "invalid.token",
            JsonData = "{}"
        };

        var result = Result<Submission>.Invalid(new ValidationError { ErrorMessage = "Invalid token" });

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
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
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            JsonData = "{}"
        };

        var result = Result<Submission>.NotFound("Submission not found");

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_TokenWithoutEditPermission_ReturnsForbidden()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.r.signature",
            JsonData = "{}"
        };

        var result = Result<Submission>.Forbidden("Token does not have edit permission");

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            IsComplete = true,
            CurrentPage = 10,
            JsonData = "{\"test\":\"data\"}",
            Metadata = "{\"meta\":\"info\"}"
        };

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            CurrentPage: 0,
            FormId: 123L,
            FormDefinitionId: 1L,
            JsonData: "{}"));
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateByAccessTokenCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.AccessToken == request.Token &&
                cmd.IsComplete == request.IsComplete &&
                cmd.CurrentPage == request.CurrentPage &&
                cmd.JsonData == request.JsonData &&
                cmd.Metadata == request.Metadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            IsComplete = true,
            CurrentPage = null,
            JsonData = null,
            Metadata = null
        };

        var submission = Submission.Create(456, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            CurrentPage: 0,
            FormId: 123L,
            FormDefinitionId: 1L,
            JsonData: "{\"existing\":\"data\"}",
            IsComplete: true));
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value!.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UpdateWithAllFields_ReturnsCompleteSubmission()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            IsComplete = true,
            CurrentPage = 15,
            JsonData = "{\"name\":\"Updated Name\"}",
            Metadata = "{\"updated\":\"metadata\"}"
        };

        var updatedSubmission = Submission.Create(456, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: 123L,
            FormDefinitionId: 1L,
            JsonData: request.JsonData!,
            IsComplete: true,
            CurrentPage: 15,
            Metadata: request.Metadata));
        var result = Result.Success(updatedSubmission);

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value!.JsonData.Should().Be(request.JsonData);
        okResult!.Value!.IsComplete.Should().BeTrue();
        okResult!.Value!.CurrentPage.Should().Be(15);
        okResult!.Value!.Metadata.Should().Be(request.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            JsonData = "{}"
        };

        var result = Result<Submission>.Invalid(new ValidationError { ErrorMessage = "Token expired" });

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptionalFields_SuccessfullyUpdates()
    {
        // Arrange
        var request = new PartialUpdateByAccessTokenRequest
        {
            FormId = 123,
            Token = "12345.1234567890.w.signature",
            IsComplete = null,
            CurrentPage = null,
            JsonData = null,
            Metadata = null
        };

        var submission = Submission.Create(456, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            CurrentPage: 0,
            FormId: 123L,
            FormDefinitionId: 1L,
            JsonData: "{}"));
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<PartialUpdateByAccessTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
    }
}
