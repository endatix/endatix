using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.Create;
using Endatix.Infrastructure.ReCaptcha;
using Errors = Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateTests
{
    private readonly IMediator _mediator;

    private readonly IGoogleReCaptchaService _recaptcha;

    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _recaptcha = Substitute.For<IGoogleReCaptchaService>();
        _endpoint = Factory.Create<Create>(_mediator, _recaptcha);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new CreateSubmissionRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest<Errors.ProblemDetails>;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithSubmission()
    {
        // Arrange
        var formId = 1L;
        var jsonData = "{ }";
        var request = new CreateSubmissionRequest
        {
            FormId = formId,
            JsonData = jsonData,
            IsComplete = true,
            CurrentPage = 2,
            Metadata = "test metadata"
        };

        var submission = new Submission(SampleData.TENANT_ID, jsonData, formId, 2) { Id = 1 };
        submission.UpdateToken(new Token(1));
        var result = Result<Submission>.Created(submission);

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateSubmissionResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be("1");
        createdResult!.Value!.Token.Should().NotBeNull();
        createdResult!.Value!.Token!.Should().Be(submission.Token.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateSubmissionRequest
        {
            FormId = 123,
            IsComplete = true,
            CurrentPage = 2,
            JsonData = """{ "field": "value" }""",
            Metadata = """{ "key", "value" }""",
            ReCaptchaToken = "recaptcha-token"
        };
        var result = Result<Submission>.Created(new Submission(SampleData.TENANT_ID, """{ "field": "value" }""", 123, 456));

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateSubmissionCommand>(cmd =>
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
        var request = new CreateSubmissionRequest { FormId = 1, IsComplete = false };
        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Submission>.Created(new Submission(1, "{}", 1, 1)));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        await _recaptcha.DidNotReceive().VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var okResult = response.Result as Created<CreateSubmissionResponse>;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsReCaptcha_WhenReCaptchaIsDisabled()
    {
        // Arrange
        _recaptcha.IsEnabled.Returns(false);
        var request = new CreateSubmissionRequest { FormId = 1, IsComplete = true, ReCaptchaToken = "token" };
        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Submission>.Created(new Submission(1, "{}", 1, 1)));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        await _recaptcha.DidNotReceive().VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var okResult = response.Result as Created<CreateSubmissionResponse>;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenReCaptchaFails_AndIsComplete()
    {
        // Arrange
        _recaptcha.IsEnabled.Returns(true);
        _recaptcha.VerifyTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ReCaptchaVerificationResult.InvalidResponse(0.0, "form_submit", "invalid"));

        var request = new CreateSubmissionRequest { FormId = 1, IsComplete = true, ReCaptchaToken = "token" };

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

        var submission = new Submission(SampleData.TENANT_ID, "{}", 1, 1);
        submission.UpdateToken(new Token(1));
        var request = new CreateSubmissionRequest { FormId = 1, IsComplete = true, ReCaptchaToken = "token" };
        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Submission>.Created(submission));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateSubmissionResponse>;
        createdResult.Should().NotBeNull();
    }
}
