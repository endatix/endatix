using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.GetById;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 1L;
        var request = new GetByIdRequest { FormId = formId, SubmissionId = submissionId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetByIdQuery>(), Arg.Any<CancellationToken>())
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
        var request = new GetByIdRequest { FormId = formId, SubmissionId = submissionId };
        var result = Result.NotFound("Submission not found");

        _mediator.Send(Arg.Any<GetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithSubmission()
    {
        // Arrange
        var formId = 1L;
        var submissionId = 1L;
        var request = new GetByIdRequest { FormId = formId, SubmissionId = submissionId };
        var submission = new Submission("{ }", 1, 2) { Id = submissionId };
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<GetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<SubmissionModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(submissionId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetByIdRequest
        {
            FormId = 123,
            SubmissionId = 456
        };
        var result = Result.Success(new Submission("{ }", 1, 2));
        
        _mediator.Send(Arg.Any<GetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetByIdQuery>(query =>
                query.SubmissionId == request.SubmissionId &&
                query.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
