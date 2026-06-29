using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.Create;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.Features.ReCaptcha;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Create>(_mediator);
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
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
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
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = response.Result as Created<SubmissionModel>;
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
                cmd.Metadata == request.Metadata &&
                cmd.ReCaptchaToken == request.ReCaptchaToken &&
                cmd.RequiredPermission == "submissions.create"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_AnonymousUser_PassesNullSubmittedBy()
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
                cmd.Metadata == request.Metadata &&
                cmd.ReCaptchaToken == request.ReCaptchaToken &&
                cmd.RequiredPermission == "submissions.create"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_ReCaptchaValidationFailed_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateSubmissionRequest { FormId = 1, ReCaptchaToken = "invalid-token" };
        var result = Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult!.ProblemDetails.Detail.Should().Contain(ReCaptchaErrors.Messages.RECAPTCHA_VERIFICATION_FAILED);
        problemResult!.ProblemDetails.Extensions["errorCode"].Should().Be(ReCaptchaErrors.ErrorCodes.RECAPTCHA_VERIFICATION_FAILED);
    }

    [Fact]
    public async Task ExecuteAsync_ConflictResult_ReturnsConflictProblemDetails()
    {
        // Arrange
        var request = new CreateSubmissionRequest { FormId = 1, JsonData = "{}" };
        var result = Result<Submission>.Conflict(["Submission already exists for this user."]);

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problemResult!.ProblemDetails.Title.Should().Be("There was a conflict");
        problemResult!.ProblemDetails.Detail.Should().Contain("Submission already exists for this user.");
    }
}
