using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using Endatix.Infrastructure.ReCaptcha;
using Errors = Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateByTokenTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdateByToken _endpoint;

    private readonly IGoogleReCaptchaService _recaptcha;

    public PartialUpdateByTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _recaptcha = Substitute.For<IGoogleReCaptchaService>();
        _endpoint = Factory.Create<PartialUpdateByToken>(_mediator, _recaptcha);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionToken = "invalid-token";
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest<Errors.ProblemDetails>;
        badRequestResult.Should().NotBeNull();
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
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
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

    [Fact]
    public async Task ExecuteAsync_SkipsReCaptcha_WhenFormIsNotComplete()
    {
        // Arrange
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = 1, SubmissionToken = "token", IsComplete = false };
        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Submission(1, "{}", 1, 1)));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        await _recaptcha.DidNotReceive().VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsReCaptcha_WhenReCaptchaIsDisabled()
    {
        // Arrange
        _recaptcha.IsEnabled.Returns(false);
        var request = new PartialUpdateSubmissionByTokenRequest { FormId = 1, SubmissionToken = "token", IsComplete = true, ReCaptchaToken = "token" };
        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Submission(1, "{}", 1, 1)));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        await _recaptcha.DidNotReceive().VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenReCaptchaFails_AndIsComplete()
    {
        // Arrange
        _recaptcha.IsEnabled.Returns(true);
        _recaptcha.VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ReCaptchaVerificationResult.InvalidResponse(0.0, "form_submit", "invalid"));

        var request = new PartialUpdateSubmissionByTokenRequest { FormId = 1, SubmissionToken = "token", IsComplete = true, ReCaptchaToken = "token" };

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest<Errors.ProblemDetails>;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Proceeds_WhenReCaptchaSucceeds_AndIsComplete()
    {
        // Arrange
        _recaptcha.IsEnabled.Returns(true);
        _recaptcha.VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ReCaptchaVerificationResult.Success(1.0, "form_submit"));

        var request = new PartialUpdateSubmissionByTokenRequest { FormId = 1, SubmissionToken = "token", IsComplete = true, ReCaptchaToken = "token" };
        _mediator.Send(Arg.Any<PartialUpdateSubmissionByTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Submission(1, "{}", 1, 1)));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionByTokenResponse>;
        okResult.Should().NotBeNull();
    }
}
