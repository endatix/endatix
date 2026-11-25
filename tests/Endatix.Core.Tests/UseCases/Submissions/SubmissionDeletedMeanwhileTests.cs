using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.Delete;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Core.Tests.UseCases.Submissions;

/// <summary>
/// A save that finds the submission deleted since it was read, on its own or with its form, is not found, and
/// nothing is published for it.
/// </summary>
public class SubmissionDeletedMeanwhileTests
{
    private const long FormId = 2;
    private const long SubmissionId = 1;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IRepository<SubmissionVersion> _versions = Substitute.For<IRepository<SubmissionVersion>>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly Submission _submission = Submission.Create(SubmissionId, new SubmissionCreateArgs(
        TenantId: SampleData.TENANT_ID,
        FormId: FormId,
        FormDefinitionId: 3,
        JsonData: "{}",
        IsComplete: false));

    public SubmissionDeletedMeanwhileTests()
    {
        _submissions.SingleOrDefaultAsync(Arg.Any<SubmissionByFormIdAndSubmissionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(_submission);
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new SubmissionDeletedException("deleted"));
    }

    [Fact]
    public async Task PartialUpdateHandle_CompletingASubmissionDeletedMeanwhile_ReturnsNotFoundWithoutPublishing()
    {
        // Arrange
        var handler = new PartialUpdateSubmissionHandler(_submissions, _versions, _mediator);
        var command = new PartialUpdateSubmissionCommand(SubmissionId, FormId, true, null, """{"q":"done"}""", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _mediator.DidNotReceive().Publish(Arg.Any<SubmissionCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateHandle_SubmissionDeletedMeanwhile_ReturnsNotFoundWithoutPublishing()
    {
        // Arrange
        var handler = new UpdateSubmissionHandler(_submissions, _versions, _mediator);
        var command = new UpdateSubmissionCommand(SubmissionId, FormId, true, 1, """{"q":"done"}""", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _mediator.DidNotReceive().Publish(Arg.Any<SubmissionCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusHandle_SubmissionDeletedMeanwhile_ReturnsNotFound()
    {
        // Arrange
        var handler = new UpdateStatusHandler(_submissions, NullLogger<UpdateStatusHandler>.Instance);
        var command = new UpdateStatusCommand(SubmissionId, FormId, SubmissionStatusCodes.Read);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteHandle_SubmissionDeletedMeanwhile_ReturnsNotFound()
    {
        // Arrange
        var handler = new DeleteSubmissionHandler(_submissions);
        var command = new DeleteSubmissionCommand(formId: FormId, submissionId: SubmissionId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateStatusHandle_SaveChosenAsDeadlockVictim_ReturnsUnavailable()
    {
        // Arrange
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new DeadlockVictimException("deadlock"));
        var handler = new UpdateStatusHandler(_submissions, NullLogger<UpdateStatusHandler>.Instance);
        var command = new UpdateStatusCommand(SubmissionId, FormId, SubmissionStatusCodes.Read);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
    }

    [Fact]
    public async Task PartialUpdateHandle_SaveChosenAsDeadlockVictim_ReturnsUnavailableWithoutPublishing()
    {
        // Arrange
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new DeadlockVictimException("deadlock"));
        var handler = new PartialUpdateSubmissionHandler(_submissions, _versions, _mediator);
        var command = new PartialUpdateSubmissionCommand(SubmissionId, FormId, true, null, """{"q":"done"}""", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _mediator.DidNotReceive().Publish(Arg.Any<SubmissionCompletedEvent>(), Arg.Any<CancellationToken>());
    }
}
