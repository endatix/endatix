using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Audience.Features.BackgroundJobs;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Audience.Tests.Features.BackgroundJobs;

/// <summary>
/// The job side of Audience's outbox work. The inline handlers are built without a database: a path that reached
/// the work would throw, so a result proves the work was not run.
/// </summary>
public sealed class AudienceOutboxJobHandlerTests
{
    private const long TenantA = 11;
    private const long MessageId = 700;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly List<OutboxMessage> _messages = [];

    public AudienceOutboxJobHandlerTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());
    }

    [Fact]
    public async Task ExecuteAsync_MessageGone_FailsWithoutRetry()
    {
        // Arrange — no row: the outbox cleanup removed it.
        var handler = CreateReleaseLinkHandler();

        // Act
        var result = await handler.ExecuteAsync(ReleaseLinkJob(TenantA), TestContext.Current.CancellationToken);

        // Assert — a failure result, not a throw, so the job is not retried.
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(ReleaseLinkOnTestMarkJobHandler.MessageGone);
    }

    [Fact]
    public async Task ExecuteAsync_MessageOfOtherTenant_FailsAsGone()
    {
        // Arrange — the job is tenant A's; the message is not.
        Seed("submission.updated", """{"tenantId":"12","submissionId":"2","changeKind":"test"}""", 12);
        var handler = CreateReleaseLinkHandler();

        // Act
        var result = await handler.ExecuteAsync(ReleaseLinkJob(TenantA), TestContext.Current.CancellationToken);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(ReleaseLinkOnTestMarkJobHandler.MessageGone);
    }

    [Theory]
    [InlineData("""{"tenantId":"12","submissionId":"2","changeKind":"test"}""")]
    [InlineData("""{"tenantId":"11","changeKind":"test"}""")]
    [InlineData("not json")]
    public async Task ExecuteAsync_UnreadableMessage_FailsWithoutRetry(string payload)
    {
        // Arrange — the row is tenant A's, but its payload names another tenant, lacks the submission, or is not JSON.
        Seed("submission.updated", payload, TenantA);
        var handler = CreateReleaseLinkHandler();

        // Act
        var result = await handler.ExecuteAsync(ReleaseLinkJob(TenantA), TestContext.Current.CancellationToken);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(ReleaseLinkOnTestMarkJobHandler.MessageUnreadable);
    }

    [Fact]
    public async Task ExecuteAsync_ChangeThatLeftTheTestFlag_CompletesWithoutWork()
    {
        // Arrange — only the answers changed.
        Seed("submission.updated", $$"""{"tenantId":"{{TenantA}}","submissionId":"2","changeKind":"answers"}""", TenantA);
        var handler = CreateReleaseLinkHandler();

        // Act
        var result = await handler.ExecuteAsync(ReleaseLinkJob(TenantA), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DeletedFormMessageOfOtherTenantPayload_FailsWithoutRetry()
    {
        // Arrange
        Seed("form.deleted", """{"tenantId":"12","formId":"1"}""", TenantA);
        var handler = new DeleteFormAudienceJobHandler(
            _outbox, new DeleteFormAudienceOutboxHandler(null!), NullLogger<DeleteFormAudienceJobHandler>.Instance);
        var job = new BackgroundJobContext(1, AudienceDeleteFormAudiencePayload.JobType, TenantA,
            BackgroundJobPayloadSerializer.Serialize(new AudienceDeleteFormAudiencePayload(MessageId)), 1);

        // Act
        var result = await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(DeleteFormAudienceJobHandler.MessageUnreadable);
    }

    private ReleaseLinkOnTestMarkJobHandler CreateReleaseLinkHandler() =>
        new(_outbox, new ReleaseLinkOnTestMarkOutboxHandler(null!, null!), NullLogger<ReleaseLinkOnTestMarkJobHandler>.Instance);

    private void Seed(string eventType, string payload, long tenantId) =>
        _messages.Add(new OutboxMessage(eventType, payload, tenantId, DateTime.UtcNow, 1) { Id = MessageId });

    private static BackgroundJobContext ReleaseLinkJob(long tenantId) =>
        new(1, AudienceReleaseLinkOnTestMarkPayload.JobType, tenantId,
            BackgroundJobPayloadSerializer.Serialize(new AudienceReleaseLinkOnTestMarkPayload(MessageId)), 1);
}
