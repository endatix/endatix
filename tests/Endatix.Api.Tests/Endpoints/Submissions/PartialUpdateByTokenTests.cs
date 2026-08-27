using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateByTokenTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdateByToken _endpoint;

    public PartialUpdateByTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdateByToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionToken = "invalid-token";
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var result = Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.ProblemDetails.Detail.Trim().Should().Be(SubmissonTokenErrors.Messages.SUBMISSION_TOKEN_INVALID);
        problemResult!.ProblemDetails.Extensions["errorCode"].Should().Be(SubmissonTokenErrors.ErrorCodes.SUBMISSION_TOKEN_INVALID);
    }

    [Fact]
    public async Task ExecuteAsync_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var submissionToken = "valid-token";
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var result = Result.NotFound("Invalid or expired token");

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ReCaptchaValidationFailed_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionToken = "valid-token";
        var reCaptchaToken = "invalid-token";   
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = formId, SubmissionToken = submissionToken, ReCaptchaToken = reCaptchaToken };
        var result = Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.ProblemDetails.Detail.Trim().Should().Be(ReCaptchaErrors.Messages.RECAPTCHA_VERIFICATION_FAILED);
        problemResult!.ProblemDetails.Extensions["errorCode"].Should().Be(ReCaptchaErrors.ErrorCodes.RECAPTCHA_VERIFICATION_FAILED);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedSubmission()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var submissionToken = "valid-token";
        var jsonData = "{ }";
        var request = new PartialUpdateSubmissionByTokenRequest
        {
            FormId = formId,
            SubmissionToken = submissionToken,
            JsonData = jsonData,
            IsComplete = true,
            CurrentPage = 2,
            Metadata = "test metadata"
        };

        var submission = new Submission(SampleData.TENANT_ID, jsonData, formId, formDefinitionId) { Id = 1 };
        submission.UpdateToken(new Token(1));
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be("1");
        okResult!.Value!.Token.Should().NotBeNull();
        okResult!.Value!.Token!.Should().Be(submission.Token.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateSubmissionByTokenRequest
        {
            FormId = 123,
            SubmissionToken = "token-456",
            IsComplete = true,
            CurrentPage = 2,
            JsonData = """{ "field": "value" }""",
            Metadata = """{ "key", "value" }""",
            ReCaptchaToken = "recaptcha-token"
        };
        var formDefinitionId = 456;
        var result = Result.Success(new Submission(SampleData.TENANT_ID, request.JsonData, request.FormId, formDefinitionId));

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateSubmissionByTokenCommand>(cmd =>
                cmd.Token == request.SubmissionToken &&
                cmd.FormId == request.FormId &&
                cmd.IsComplete == request.IsComplete &&
                cmd.CurrentPage == request.CurrentPage &&
                cmd.JsonData == request.JsonData &&
                cmd.Metadata == request.Metadata
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
