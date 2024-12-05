using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.PartialUpdate;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class PartialUpdateTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdate>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 1L;
        var request = new PartialUpdateSubmissionRequest { FormId = formId, SubmissionId = submissionId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 1L;
        var request = new PartialUpdateSubmissionRequest { FormId = formId, SubmissionId = submissionId };
        var result = Result.NotFound("Submission not found");

        _mediator.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
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
        var submissionId = 1L;
        var jsonData = "{ }";
        var request = new PartialUpdateSubmissionRequest 
        { 
            FormId = formId,
            SubmissionId = submissionId,
            JsonData = jsonData,
            IsComplete = true,
            CurrentPage = 2,
            Metadata = "test metadata"
        };
        
        var submission = new Submission(jsonData) { Id = submissionId };
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateSubmissionResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(submissionId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateSubmissionRequest
        {
            FormId = 123,
            SubmissionId = 456,
            IsComplete = true,
            CurrentPage = 2,
            JsonData = """{ "field": "value" }""",
            Metadata = """{ "key": "value" }"""
        };
        var result = Result.Success(new Submission());
        
        _mediator.Send(Arg.Any<PartialUpdateSubmissionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateSubmissionCommand>(cmd =>
                cmd.SubmissionId == request.SubmissionId &&
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
