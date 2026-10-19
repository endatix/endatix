using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.SetTest;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class SetTestTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly SetTest _endpoint;

    public SetTestTests()
    {
        _endpoint = Factory.Create<SetTest>(_mediator);
    }

    private static SubmissionDto Dto(bool isTest) =>
        new(2, false, "{}", 1, 1, null, DateTime.UtcNow, null, DateTime.UtcNow, null, null, "new", null, null, null, null, isTest);

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsMarkedSubmissionAndSendsCommand()
    {
        // Arrange
        _mediator.Send(Arg.Any<SetSubmissionTestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Dto(isTest: true)));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2, IsTest = true }, CancellationToken.None);

        // Assert
        var ok = response.Result.Should().BeOfType<Ok<SubmissionModel>>().Subject;
        ok.Value!.IsTestSubmission.Should().BeTrue();
        await _mediator.Received(1).Send(
            Arg.Is<SetSubmissionTestCommand>(c => c.FormId == 1 && c.SubmissionId == 2 && c.IsTest),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_Returns404Problem()
    {
        // Arrange
        _mediator.Send(Arg.Any<SetSubmissionTestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionDto>.NotFound("Submission not found"));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2, IsTest = true }, CancellationToken.None);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Conflict_Returns409ProblemWithExistingId()
    {
        // Arrange
        _mediator.Send(Arg.Any<SetSubmissionTestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionDto>.Conflict(
                new ExistingResource("submission_exists", "submissionId", "40"), "A submission already exists."));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2, IsTest = false }, CancellationToken.None);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Extensions["errorCode"].Should().Be("submission_exists");
    }
}
