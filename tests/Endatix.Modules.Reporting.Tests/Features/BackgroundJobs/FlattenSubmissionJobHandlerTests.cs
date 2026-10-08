using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.BackgroundJobs;

public sealed class FlattenSubmissionJobHandlerTests
{
    private const long TenantA = 11;
    private const long MessageId = 700;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly ISubmissionFlatteningProcessor _flattening = Substitute.For<ISubmissionFlatteningProcessor>();
    private readonly List<OutboxMessage> _messages = [];

    public FlattenSubmissionJobHandlerTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());
    }

    [Fact]
    public async Task ExecuteAsync_ChangeKindWithoutDataChange_CompletesWithoutFlatten()
    {
        // Arrange — only the metadata changed, which the flattened row does not hold.
        Seed("submission.updated", $$"""{"tenantId":"{{TenantA}}","formId":"1","submissionId":"2","changeKind":"metadata"}""", TenantA);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(TenantA), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _flattening.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, default);
    }

    [Fact]
    public async Task ExecuteAsync_TenantMismatch_ReturnsFailure()
    {
        // Arrange — the row is tenant A's, but its payload names another tenant.
        Seed("submission.completed", """{"tenantId":"12","formId":"1","submissionId":"2"}""", TenantA);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(TenantA), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _flattening.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, default);
    }

    [Fact]
    public async Task ExecuteAsync_CompletedSubmission_Flattens()
    {
        // Arrange
        Seed("submission.completed", $$"""{"tenantId":"{{TenantA}}","formId":"1","submissionId":"2"}""", TenantA);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(TenantA), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _flattening.Received(1).ProcessAsync(TenantA, 1, 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MessageOfOtherTenant_ReturnsFailure()
    {
        // Arrange — the job is tenant A's; the message is not.
        Seed("submission.completed", """{"tenantId":"12","formId":"1","submissionId":"2"}""", 12);
        var handler = CreateHandler();

        // Act
        var result = await handler.ExecuteAsync(Job(TenantA), CancellationToken.None);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The outbox message no longer exists.");
    }

    [Fact]
    public async Task ExecuteAsync_SchemaRebuildLockTimesOut_ThrowsSoTheJobIsRetried()
    {
        // Arrange — another rebuild of the form held the schema lock for longer than the wait allows.
        Seed("submission.completed", $$"""{"tenantId":"{{TenantA}}","formId":"1","submissionId":"2"}""", TenantA);
        TransactionLockRequest request = new(TransactionLockScopes.ReportingFormSchema, $"{TenantA}:1");
        _flattening.ProcessAsync(TenantA, 1, 2, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TransactionLockTimeoutException(request, new TimeoutException())));
        var handler = CreateHandler();

        // Act
        var act = () => handler.ExecuteAsync(Job(TenantA), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TransactionLockTimeoutException>();
    }

    private FlattenSubmissionJobHandler CreateHandler() =>
        new(
            _outbox,
            new FlattenSubmissionOutboxHandler(_flattening, NullLogger<FlattenSubmissionOutboxHandler>.Instance),
            NullLogger<FlattenSubmissionJobHandler>.Instance);

    private void Seed(string eventType, string payload, long tenantId)
    {
        var message = new OutboxMessage(eventType, payload, tenantId, DateTime.UtcNow, 1) { Id = MessageId };
        _messages.Add(message);
    }

    private static BackgroundJobContext Job(long tenantId) =>
        new(1, ReportingFlattenSubmissionPayload.JobType, tenantId,
            BackgroundJobPayloadSerializer.Serialize(new ReportingFlattenSubmissionPayload(MessageId)), 1);
}
