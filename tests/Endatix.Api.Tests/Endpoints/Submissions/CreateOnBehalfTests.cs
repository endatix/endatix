using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Core.UseCases.Submissions.Create;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class CreateOnBehalfTests
{
    private readonly IMediator _mediator;
    private readonly CreateOnBehalf _endpoint;

    public CreateOnBehalfTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<CreateOnBehalf>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new CreateSubmissionOnBehalfRequest { FormId = formId };
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
        var request = new CreateSubmissionOnBehalfRequest
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
    public async Task ExecuteAsync_WithSubmitter_UsesTrustedSubmitterPayload()
    {
        // Arrange
        const string targetUserId = "target-user-456";
        var request = new CreateSubmissionOnBehalfRequest
        {
            FormId = 123,
            JsonData = """{ "field": "value" }""",
            Submitter = new SubmitterInput(
                ExternalSubjectId: targetUserId,
                DisplayId: targetUserId,
                AuthProvider: SubmitterAuthProviders.Integration)
        };
        var result = Result<Submission>.Created(
            new Submission(SampleData.TENANT_ID, """{ "field": "value" }""", 123, 456));

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateSubmissionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.JsonData == request.JsonData &&
                cmd.Submitter == request.Submitter &&
                cmd.RequiredPermission == "submissions.create.onbehalf"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithoutSubmitter_UsesNull()
    {
        // Arrange
        var request = new CreateSubmissionOnBehalfRequest
        {
            FormId = 123,
            JsonData = """{ "field": "value" }"""
        };
        var result = Result<Submission>.Created(
            new Submission(SampleData.TENANT_ID, """{ "field": "value" }""", 123, 456));

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateSubmissionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.JsonData == request.JsonData &&
                cmd.Submitter == null &&
                cmd.RequiredPermission == "submissions.create.onbehalf"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseCorrectPermission()
    {
        // Arrange
        var request = new CreateSubmissionOnBehalfRequest
        {
            FormId = 123,
            JsonData = """{ "field": "value" }"""
        };
        var result = Result<Submission>.Created(
            new Submission(SampleData.TENANT_ID, """{ "field": "value" }""", 123, 456));

        _mediator.Send(Arg.Any<CreateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateSubmissionCommand>(cmd =>
                cmd.RequiredPermission == "submissions.create.onbehalf"
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
