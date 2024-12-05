using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.Create;

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
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
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
        
        var submission = new Submission(jsonData) { Id = 1, Token = new Token(1) };
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
            Metadata = """{ "key", "value" }"""
        };
        var result = Result<Submission>.Created(new Submission());
        
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
}
