using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class UpdateStatusTests
{
    private readonly IMediator _mediator;
    private readonly UpdateStatus _endpoint;

    public UpdateStatusTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<UpdateStatus>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldReturnOkResult()
    {
        // Arrange
        const long submissionId = 1;
        const long formId = 2;
        const string status = "seen";

        var request = new UpdateStatusRequest(submissionId, formId, status);
        var result = Result.Success(new SubmissionDto(submissionId, false, "{}", formId, 1, null, DateTime.UtcNow, DateTime.UtcNow, null, status, "7"));

        _mediator.Send(Arg.Any<UpdateStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.Should().BeOfType<Ok<UpdateStatusResponse>>().Subject;
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.SubmissionId.Should().Be(submissionId);
        okResult.Value?.Status.Should().Be(status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        var request = new UpdateStatusRequest(1, 2, "Approved");
        var result = Result.NotFound("Submission not found");

        _mediator.Send(Arg.Any<UpdateStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        response.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidRequest_ShouldReturnBadRequestResult()
    {
        // Arrange
        var request = new UpdateStatusRequest(1, 2, "Invalid");
        var result = Result.Invalid(new ValidationError("Invalid status"));

        _mediator.Send(Arg.Any<UpdateStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        response.Result.Should().BeOfType<BadRequest>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectCommandToMediator()
    {
        // Arrange
        const long submissionId = 1;
        const long formId = 2;
        const string status = "Approved";

        var request = new UpdateStatusRequest(submissionId, formId, status);
        var result = Result.Success(new SubmissionDto(submissionId, false, "{}", formId, 1, null, DateTime.UtcNow, DateTime.UtcNow, null, status, null));

        _mediator.Send(Arg.Any<UpdateStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateStatusCommand>(cmd =>
                cmd.SubmissionId == submissionId &&
                cmd.FormId == formId &&
                cmd.StatusCode == status
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
