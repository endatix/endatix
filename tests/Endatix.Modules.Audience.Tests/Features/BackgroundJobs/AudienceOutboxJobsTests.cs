using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Features.BackgroundJobs;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Tests.Features.BackgroundJobs;

/// <summary>
/// Audience's outbox work run as jobs, as the host registers them. The inline handlers are built without a database:
/// a path that reached the work would throw, so a result proves the work was not run.
/// </summary>
public sealed class AudienceOutboxJobsTests : IDisposable
{
    private const long TenantA = 11;
    private const long MessageId = 700;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly List<OutboxMessage> _messages = [];
    private readonly ServiceProvider _provider;

    public AudienceOutboxJobsTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_outbox);
        services.AddAudienceOutboxWork();
        services.AddScoped(_ => new DeleteFormAudienceOutboxHandler(null!));
        services.AddScoped(_ => new ReleaseLinkOnTestMarkOutboxHandler(null!, null!));
        _provider = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("test", false)]
    [InlineData("answers", true)]
    [InlineData("metadata", true)]
    public void ReleaseLinkRead_SubmissionUpdate_HasWorkOnlyForATestMark(string changeKind, bool expectedNone)
    {
        // Arrange — with OnlyWhenThereIsWork, a message read as None enqueues no job.
        var message = new Message($$"""{"submissionId":"20","tenantId":"{{TenantA}}","changeKind":"{{changeKind}}"}""");

        // Act
        var work = new ReleaseLinkOnTestMarkOutboxHandler(null!, null!).Read(message);

        // Assert
        work.IsNone.Should().Be(expectedNone);
    }

    [Theory]
    [InlineData("""{"tenantId":"12","submissionId":"2","changeKind":"test"}""")]
    [InlineData("""{"tenantId":"11","changeKind":"test"}""")]
    [InlineData("not json")]
    public async Task ReleaseLinkJob_UnreadableMessage_FailsWithoutRetry(string payload)
    {
        // Arrange — the row is tenant A's, but its payload names another tenant, lacks the submission, or is not JSON.
        Seed("submission.updated", payload);

        // Act
        var result = await RunAsync(new AudienceReleaseLinkOnTestMarkPayload(MessageId));

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageUnreadable);
    }

    [Fact]
    public async Task ReleaseLinkJob_ChangeThatLeftTheTestFlag_CompletesWithoutWork()
    {
        // Arrange — only the answers changed.
        Seed("submission.updated", $$"""{"tenantId":"{{TenantA}}","submissionId":"2","changeKind":"answers"}""");

        // Act
        var result = await RunAsync(new AudienceReleaseLinkOnTestMarkPayload(MessageId));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFormAudienceJob_PayloadOfOtherTenant_FailsWithoutRetry()
    {
        // Arrange
        Seed("form.deleted", """{"tenantId":"12","formId":"1"}""");

        // Act
        var result = await RunAsync(new AudienceDeleteFormAudiencePayload(MessageId));

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageUnreadable);
    }

    [Theory]
    [InlineData("AudienceDeleteFormAudience")]
    [InlineData("AudienceReleaseLinkOnTestMark")]
    public async Task Job_QueuedBeforeTheOneCallRegistration_StillRunsAsItsJobType(string jobType)
    {
        // Arrange — the job type and input a job queued by the previous release holds, row gone since.
        using var scope = _provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(jobType);
        var job = new BackgroundJobContext(1, jobType, TenantA, $$"""{"outboxMessageId":{{MessageId}}}""", 1);

        // Act
        var result = await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);

        // Assert — the input was read (not "could not be read") and the job looked the row up.
        handler.JobType.Should().Be(jobType);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(OutboxJobFailures.MessageGone);
    }

    public void Dispose() => _provider.Dispose();

    private void Seed(string eventType, string payload) =>
        _messages.Add(new OutboxMessage(eventType, payload, TenantA, DateTime.UtcNow, 1) { Id = MessageId });

    private async Task<Result> RunAsync<TPayload>(TPayload payload)
        where TPayload : IBackgroundJobPayload
    {
        using var scope = _provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(TPayload.JobType);
        var job = new BackgroundJobContext(1, TPayload.JobType, TenantA, BackgroundJobPayloadSerializer.Serialize(payload), 1);
        return await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);
    }

    private sealed record Message(string Payload) : IOutboxMessage
    {
        public long Id => MessageId;
        public string EventType => "submission.updated";
        public long TenantId => TenantA;
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
