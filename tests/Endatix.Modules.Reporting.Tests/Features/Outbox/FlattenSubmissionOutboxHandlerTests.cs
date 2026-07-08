using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class FlattenSubmissionOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;
    private const long SubmissionId = 500;

    private readonly ISubmissionFlatteningProcessor _flatteningProcessor =
        Substitute.For<ISubmissionFlatteningProcessor>();

    private FlattenSubmissionOutboxHandler CreateSut() =>
        new(_flatteningProcessor, NullLogger<FlattenSubmissionOutboxHandler>.Instance);

    [Fact]
    public void EventTypes_IncludesSubmissionCompletedAndUpdated()
    {
        IReadOnlyCollection<string> eventTypes = CreateSut().EventTypes;

        eventTypes.Should().Contain(SubmissionCompletedEvent.EventTypeName);
        eventTypes.Should().Contain(SubmissionUpdatedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_WithSubmissionCompleted_CallsProcessor()
    {
        Submission submission = CreateSubmission();
        string payload = ReportingOutboxTestHelpers.SerializePayload(new SubmissionCompletedEvent(submission).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 1,
            EventType: SubmissionCompletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _flatteningProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            SubmissionId,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("answers")]
    [InlineData("definition")]
    [InlineData("answers,metadata")]
    public async Task HandleAsync_WithSubmissionUpdated_DataAffectingChangeKind_CallsProcessor(string changeKind)
    {
        Submission submission = CreateSubmission();
        string payload = ReportingOutboxTestHelpers.SerializePayload(
            new SubmissionUpdatedEvent(submission, SubmissionChangeKindsExtensions.ParseWireValue(changeKind)).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 2,
            EventType: SubmissionUpdatedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _flatteningProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            SubmissionId,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("submitter")]
    [InlineData("")]
    public async Task HandleAsync_WithSubmissionUpdated_NonDataChangeKind_DoesNotCallProcessor(string changeKind)
    {
        Submission submission = CreateSubmission();
        SubmissionChangeKinds parsedChangeKind = string.IsNullOrEmpty(changeKind)
            ? SubmissionChangeKinds.None
            : SubmissionChangeKindsExtensions.ParseWireValue(changeKind);
        string payload = ReportingOutboxTestHelpers.SerializePayload(
            new SubmissionUpdatedEvent(submission, parsedChangeKind).GetPayload());
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 3,
            EventType: SubmissionUpdatedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);

        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await _flatteningProcessor.DidNotReceive().ProcessAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingSubmissionId_ThrowsAndDoesNotCallProcessor()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 4,
            EventType: SubmissionCompletedEvent.EventTypeName,
            Payload: """{"tenantId":"42","formId":"100"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a valid submissionId*");
        await _flatteningProcessor.DidNotReceive().ProcessAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    private static Submission CreateSubmission()
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;
        return submission;
    }
}
