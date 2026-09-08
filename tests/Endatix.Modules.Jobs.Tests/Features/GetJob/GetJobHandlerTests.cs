using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Features.GetJob;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Tests.Features.GetJob;

/// <summary>
/// Runs against a real (in-memory) context so the tenant query filter is exercised rather than
/// assumed — it is the only thing standing between one tenant and another's job rows.
/// </summary>
public sealed class GetJobHandlerTests : IDisposable
{
    private const long CallerTenantId = 7;
    private const long OtherTenantId = 8;

    private readonly TestJobsDbContext _dbContext;
    private readonly GetJobHandler _handler;

    public GetJobHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseInMemoryDatabase($"jobs-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestJobsDbContext(
            options, new SequentialIdGenerator(), new FixedTenantContext(CallerTenantId));
        _handler = new GetJobHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private BackgroundJob AddJob(long tenantId = CallerTenantId, string? resultJson = null)
    {
        var job = new BackgroundJob("SubmissionExport", @"{""formId"":""1""}", tenantId, DateTime.UtcNow);
        _dbContext.BackgroundJobs.Add(job);
        _dbContext.SaveChanges();

        if (resultJson is not null)
        {
            job.Claim(DateTime.UtcNow);
            job.Complete(DateTime.UtcNow, resultJson);
            _dbContext.SaveChanges();
        }

        return job;
    }

    [Fact]
    public async Task Handle_JobInTheCallersTenant_ReturnsItsState()
    {
        // Arrange
        var job = AddJob();

        // Act
        var result = await _handler.Handle(new GetJobQuery(job.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(job.Id);
        result.Value.Type.Should().Be("SubmissionExport");
        result.Value.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        // Arrange
        // Act
        var result = await _handler.Handle(new GetJobQuery(404), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_JobOwnedByAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        var foreignJob = AddJob(OtherTenantId);

        // Act
        var result = await _handler.Handle(
            new GetJobQuery(foreignJob.Id), TestContext.Current.CancellationToken);

        // Assert — not Forbidden: a 403 would confirm the id exists, which is enough to enumerate
        // another tenant's jobs.
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_CompletedExport_ReportsTheFileItProduced()
    {
        // Arrange
        var job = AddJob(resultJson: @"{""fileName"":""submissions-100.csv"",""contentType"":""text/csv""}");

        // Act
        var result = await _handler.Handle(new GetJobQuery(job.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Result.Should().NotBeNull();
        result.Value.Result!["fileName"]!.GetValue<string>().Should().Be("submissions-100.csv");
    }

    [Fact]
    public async Task Handle_CompletedJobWithNoFileOutput_PassesItsResultThrough()
    {
        // Arrange — a webhook delivery reports a response code, not a file. Both are legitimate
        // outputs, which is why this endpoint imposes no shape on them.
        var job = AddJob(resultJson: @"{""statusCode"":200,""attempt"":1}");

        // Act
        var result = await _handler.Handle(new GetJobQuery(job.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Result!["statusCode"]!.GetValue<int>().Should().Be(200);
    }

    [Fact]
    public async Task Handle_UnfinishedJob_ReportsNoResult()
    {
        // Arrange
        var job = AddJob();

        // Act
        var result = await _handler.Handle(new GetJobQuery(job.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompletedJobWithAnUnreadableResult_StillReportsTheStatus()
    {
        // Arrange — the column belongs to whichever handler ran the job, so its contents are not
        // this endpoint's to guarantee.
        var job = AddJob(resultJson: "not json at all");

        // Act
        var result = await _handler.Handle(new GetJobQuery(job.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(JobStatus.Completed);
        result.Value.Result.Should().BeNull();
    }
}
