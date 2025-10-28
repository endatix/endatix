using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.Create;
using Endatix.Core.Abstractions;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.Features.ReCaptcha;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _userContext = Substitute.For<IUserContext>();
        _endpoint = Factory.Create<Create>(_mediator, _userContext);
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
        const string userId = "123";
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

        _userContext.GetCurrentUserId().Returns(userId);
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
                cmd.SubmittedBy == userId &&
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

        _userContext.GetCurrentUserId().Returns((string?)null);
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
                cmd.SubmittedBy == null &&
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
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult!.ProblemDetails.Detail.Should().Contain(ReCaptchaErrors.Messages.RECAPTCHA_VERIFICATION_FAILED);
        problemResult!.ProblemDetails.Extensions["errorCode"].Should().Be(ReCaptchaErrors.ErrorCodes.RECAPTCHA_VERIFICATION_FAILED);
    }
}
