using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.GetByToken;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class GetByTokenTests
{
    private readonly IMediator _mediator;
    private readonly GetByToken _endpoint;

    public GetByTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetByToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var submissionToken = "invalid-token";
        var request = new GetByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetByTokenQuery>(), Arg.Any<CancellationToken>())
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
        var submissionToken = "valid-token";
        var request = new GetByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var result = Result.NotFound("Submission not found");

        _mediator.Send(Arg.Any<GetByTokenQuery>(), Arg.Any<CancellationToken>())
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
        var submissionToken = "valid-token";
        var request = new GetByTokenRequest { FormId = formId, SubmissionToken = submissionToken };
        var submission = new Submission { Id = 1, Token = new Token(1) };
        var result = Result.Success(submission);

        _mediator.Send(Arg.Any<GetByTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<SubmissionModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be("1");
        okResult!.Value!.Token.Should().NotBeNull();
        okResult!.Value!.Token!.Should().Be(submission.Token.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetByTokenRequest
        {
            FormId = 123,
            SubmissionToken = "token-456"
        };
        var result = Result.Success(new Submission());
        
        _mediator.Send(Arg.Any<GetByTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetByTokenQuery>(query =>
                query.Token == request.SubmissionToken &&
                query.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
