using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class SyncSubmissionDeletionOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;
    private const long SubmissionId = 500;

    private readonly IFlattenedSubmissionRepository _repository =
        Substitute.For<IFlattenedSubmissionRepository>();
    private readonly IReportingUnitOfWork _unitOfWork = Substitute.For<IReportingUnitOfWork>();

    private SyncSubmissionDeletionOutboxHandler CreateSut() =>
        new(_repository, _unitOfWork, NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);

    [Fact]
    public void EventTypes_IncludesSubmissionDeleted()
    {
        CreateSut().EventTypes.Should().Contain(SubmissionDeletedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_DeletesFlattenedRowInsideTransaction()
    {
        _repository
            .DeleteBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns(1);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _repository.DeleteBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>());
            _unitOfWork.CommitTransactionAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoRowsExist_StillCommitsTransaction()
    {
        _repository
            .DeleteBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns(0);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteFails_RollsBackTransaction()
    {
        _repository
            .DeleteBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("delete failed")));
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("delete failed");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingSubmissionId_ThrowsAndDoesNotBeginTransaction()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 7,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: """{"tenantId":"42","formId":"100"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a valid submissionId*");
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteBySubmissionIdAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenantMismatch_ThrowsAndDoesNotBeginTransaction()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 8,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: """{"tenantId":"99","submissionId":"500","formId":"100"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenantId mismatch*");
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().DeleteBySubmissionIdAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    private static ReportingOutboxTestHelpers.FakeOutboxMessage CreateMessage()
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;
        string payload = ReportingOutboxTestHelpers.SerializePayload(
            new SubmissionDeletedEvent(submission).GetPayload());

        return new ReportingOutboxTestHelpers.FakeOutboxMessage(
            Id: 1,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
    }
}
