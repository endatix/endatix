using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.Delete;

namespace Endatix.Core.Tests.UseCases.Submissions.Delete;

public class DeleteSubmissionHandlerTests
{
    private readonly IRepository<Submission> _repository;
    private readonly DeleteSubmissionHandler _handler;

    public DeleteSubmissionHandlerTests()
    {
        _repository = Substitute.For<IRepository<Submission>>();
        _handler = new DeleteSubmissionHandler(_repository);
    }

    [Fact]
    public async Task Handle_SubmissionNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new DeleteSubmissionCommand(formId: 1, submissionId: 99);
        _repository.GetByIdAsync(request.SubmissionId, Arg.Any<CancellationToken>())
            .Returns((Submission?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FormIdMismatch_ReturnsNotFound()
    {
        // Arrange
        var submission = CreateSubmission(formId: 2, submissionId: 99);
        var request = new DeleteSubmissionCommand(formId: 1, submissionId: 99);
        _repository.GetByIdAsync(request.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Submission not found");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesSubmissionAndPersists()
    {
        // Arrange
        var submission = CreateSubmission(formId: 1, submissionId: 99);
        var request = new DeleteSubmissionCommand(formId: 1, submissionId: 99);
        _repository.GetByIdAsync(request.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(submission);
        submission.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(submission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_RaisesSubmissionDeletedEvent()
    {
        // Arrange
        var submission = CreateSubmission(formId: 1, submissionId: 99);
        var request = new DeleteSubmissionCommand(formId: 1, submissionId: 99);
        _repository.GetByIdAsync(request.SubmissionId, Arg.Any<CancellationToken>())
            .Returns(submission);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        submission.DomainEvents.OfType<SubmissionDeletedEvent>().Should().ContainSingle();
    }

    private static Submission CreateSubmission(long formId, long submissionId)
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: formId,
            FormDefinitionId: 2,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = submissionId;
        return submission;
    }
}
