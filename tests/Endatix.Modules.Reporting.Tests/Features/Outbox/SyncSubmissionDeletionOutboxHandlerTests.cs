using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class SyncSubmissionDeletionOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;
    private const long SubmissionId = 500;

    private readonly IFlattenedSubmissionRepository _repository =
        Substitute.For<IFlattenedSubmissionRepository>();

    private SyncSubmissionDeletionOutboxHandler CreateSut() =>
        new(_repository, NullLogger<SyncSubmissionDeletionOutboxHandler>.Instance);

    [Fact]
    public void EventTypes_IncludesSubmissionDeleted()
    {
        CreateSut().EventTypes.Should().Contain(SubmissionDeletedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_WhenRowExists_MarksDeletedAndSaves()
    {
        FlattenedSubmissionRow row = new(SubmissionId, TenantId, FormId);
        _repository
            .GetBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns(row);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        row.IsDeleted.Should().BeTrue();
        await _repository.Received(1).SaveAsync(row, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRowMissing_DoesNotSave()
    {
        _repository
            .GetBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns((FlattenedSubmissionRow?)null);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _repository.DidNotReceive().SaveAsync(
            Arg.Any<FlattenedSubmissionRow>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingSubmissionId_ThrowsAndDoesNotQueryRepository()
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
        await _repository.DidNotReceive().GetBySubmissionIdAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenantMismatch_ThrowsAndDoesNotQueryRepository()
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
        await _repository.DidNotReceive().GetBySubmissionIdAsync(
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
        string payload = ReportingOutboxTestHelpers.SerializePayload(new SubmissionDeletedEvent(submission).GetPayload());

        return new ReportingOutboxTestHelpers.FakeOutboxMessage(
            Id: 1,
            EventType: SubmissionDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
    }
}
