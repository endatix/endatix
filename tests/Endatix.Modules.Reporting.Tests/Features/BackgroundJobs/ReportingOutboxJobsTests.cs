using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Reporting.Tests.Features.BackgroundJobs;

/// <summary>Reporting's outbox work run as jobs, as the host registers them.</summary>
public sealed class ReportingOutboxJobsTests : IDisposable
{
    private const long TenantA = 11;
    private const long MessageId = 700;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly ISubmissionFlatteningProcessor _flattening = Substitute.For<ISubmissionFlatteningProcessor>();
    private readonly IDefaultExportFormatsSeeder _seeder = Substitute.For<IDefaultExportFormatsSeeder>();
    private readonly List<OutboxMessage> _messages = [];
    private readonly ServiceProvider _provider;

    public ReportingOutboxJobsTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_outbox);
        services.AddSingleton(_flattening);
        services.AddSingleton(_seeder);
        services.AddSingleton(Substitute.For<IFormSchemaProcessor>());
        services.AddSingleton(Substitute.For<IFormSchemaRepository>());
        services.AddSingleton(Substitute.For<IFlattenedSubmissionRepository>());
        services.AddSingleton(Substitute.For<ISubmissionViewRepository>());
        services.AddSingleton(Substitute.For<IReportingUnitOfWork>());
        services.AddReportingOutboxWork();
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task FlattenJob_ChangeKindWithoutDataChange_CompletesWithoutFlatten()
    {
        // Arrange — only the metadata changed, which the flattened row does not hold.
        Seed("submission.updated", $$"""{"tenantId":"{{TenantA}}","formId":"1","submissionId":"2","changeKind":"metadata"}""", TenantA);

        // Act
        var result = await RunAsync(FlattenSubmissionOutboxHandler.JobType, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _flattening.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlattenJob_CompletedSubmission_Flattens()
    {
        // Arrange
        Seed("submission.completed", $$"""{"tenantId":"{{TenantA}}","formId":"1","submissionId":"2"}""", TenantA);

        // Act
        var result = await RunAsync(FlattenSubmissionOutboxHandler.JobType, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _flattening.Received(1).ProcessAsync(TenantA, 1, 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlattenJob_PayloadOfOtherTenant_FailsAsUnreadableWithoutRetry()
    {
        // Arrange — the row is tenant A's, but its payload names another tenant.
        Seed("submission.completed", """{"tenantId":"12","formId":"1","submissionId":"2"}""", TenantA);

        // Act
        var result = await RunAsync(FlattenSubmissionOutboxHandler.JobType, TenantA);

        // Assert — a failure result, not a throw, so the job is not retried.
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The outbox message could not be read.");
        await _flattening.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlattenJob_MessageOfOtherTenant_FailsAsGone()
    {
        // Arrange — the job is tenant A's; the row is not.
        Seed("submission.completed", """{"tenantId":"12","formId":"1","submissionId":"2"}""", 12);

        // Act
        var result = await RunAsync(FlattenSubmissionOutboxHandler.JobType, TenantA);

        // Assert
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The outbox message no longer exists.");
    }

    [Fact]
    public async Task SeedJob_AppLevelMessage_SeedsTheTenantBeingCreated()
    {
        // Arrange — a new tenant's row is app-level; the job is the new tenant's.
        Seed("tenant.created", $$"""{"tenantId":"{{TenantA}}"}""", 0);

        // Act
        var result = await RunAsync(SeedDefaultExportFormatsOutboxHandler.JobType, TenantA);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _seeder.Received(1).SeedAsync(TenantA, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ReportingCompileFormSchema")]
    [InlineData("ReportingFlattenSubmission")]
    [InlineData("ReportingSeedDefaultExportFormats")]
    [InlineData("ReportingSyncFormDeletion")]
    [InlineData("ReportingSyncSubmissionDeletion")]
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
            .Which.ErrorMessage.Should().Be("The outbox message no longer exists.");
    }

    public void Dispose() => _provider.Dispose();

    private void Seed(string eventType, string payload, long tenantId) =>
        _messages.Add(new OutboxMessage(eventType, payload, tenantId, DateTime.UtcNow, 1) { Id = MessageId });

    private async Task<Result> RunAsync(string jobType, long tenantId)
    {
        using var scope = _provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(jobType);
        var job = new BackgroundJobContext(1, jobType, tenantId, $$"""{"outboxMessageId":{{MessageId}}}""", 1);
        return await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);
    }
}
