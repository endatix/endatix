using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class SyncFormDeletionOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;

    private readonly IFormSchemaRepository _formSchemaRepository =
        Substitute.For<IFormSchemaRepository>();
    private readonly IFlattenedSubmissionRepository _flattenedSubmissionRepository =
        Substitute.For<IFlattenedSubmissionRepository>();
    private readonly IReportingUnitOfWork _unitOfWork = Substitute.For<IReportingUnitOfWork>();

    private SyncFormDeletionOutboxHandler CreateSut() =>
        new(
            _formSchemaRepository,
            _flattenedSubmissionRepository,
            _unitOfWork,
            NullLogger<SyncFormDeletionOutboxHandler>.Instance);

    [Fact]
    public void EventTypes_IncludesFormDeleted()
    {
        CreateSut().EventTypes.Should().Contain(FormDeletedEvent.EventTypeName);
    }

    [Fact]
    public async Task HandleAsync_DeletesSchemaAndFlattenedRowsInsideTransaction()
    {
        // Arrange
        _formSchemaRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(1);
        _flattenedSubmissionRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(3);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        // Act
        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _formSchemaRepository.DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>());
            _flattenedSubmissionRepository.DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>());
            _unitOfWork.CommitTransactionAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoRowsExist_StillCommitsTransaction()
    {
        // Arrange
        _formSchemaRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(0);
        _flattenedSubmissionRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(0);
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        // Act
        await CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteFails_RollsBackTransaction()
    {
        // Arrange
        _formSchemaRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(1);
        _flattenedSubmissionRepository
            .DeleteByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("delete failed")));
        ReportingOutboxTestHelpers.FakeOutboxMessage message = CreateMessage();

        // Act
        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("delete failed");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMissingFormId_ThrowsAndDoesNotBeginTransaction()
    {
        ReportingOutboxTestHelpers.FakeOutboxMessage message = new(
            Id: 7,
            EventType: FormDeletedEvent.EventTypeName,
            Payload: """{"tenantId":"42","name":"gone"}""",
            TenantId: TenantId);

        Func<Task> act = () => CreateSut().HandleAsync(message, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a valid formId*");
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    private static ReportingOutboxTestHelpers.FakeOutboxMessage CreateMessage()
    {
        Form form = new(TenantId, "to-delete") { Id = FormId };
        string payload = ReportingOutboxTestHelpers.SerializePayload(
            new FormDeletedEvent(form).GetPayload());

        return new ReportingOutboxTestHelpers.FakeOutboxMessage(
            Id: 1,
            EventType: FormDeletedEvent.EventTypeName,
            Payload: payload,
            TenantId: TenantId);
    }
}
