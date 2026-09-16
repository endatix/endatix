using System.Net;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Jobs;

/// <summary>
/// The job status read over HTTP, against the real tenant middleware and a real PostgreSQL query
/// filter. Unit tests cover the handler's own guard; they cannot cover this, because an in-memory
/// context is neither <c>TenantMiddleware</c> nor the database that enforces the filter.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class JobStatusApiFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task Tenant_admin_reads_a_job_in_their_own_tenant_and_not_one_in_another()
    {
        // Arrange — the module is PostgreSQL-only, so the flag is off on a SQL Server run.
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "Background jobs are PostgreSQL-only; the module is not registered on this provider.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        var tenantIds = world.SeedResult!.TenantIds;
        var ownJobId = await SeedJobAsync(world, tenantIds[0], cancellationToken);
        var foreignJobId = await SeedJobAsync(world, tenantIds[1], cancellationToken);

        // Only Admin and PlatformAdmin pass — they satisfy any permission check without a grant.
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);

        // Act
        using var own = await client.GetAsync(JobUri(ownJobId), cancellationToken);
        using var foreign = await client.GetAsync(JobUri(foreignJobId), cancellationToken);

        // Assert
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await own.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        body.GetProperty("id").GetString().Should().Be(ownJobId.ToString());
        body.GetProperty("status").GetString().Should().Be("Pending");

        // Not Forbidden: a 403 would confirm the id exists, which is enough to enumerate another
        // tenant's jobs.
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_callers_are_refused()
    {
        // Arrange
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "Background jobs are PostgreSQL-only; the module is not registered on this provider.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        var jobId = await SeedJobAsync(world, world.SeedResult!.TenantIds[0], cancellationToken);
        using var client = world.AnonymousClient();

        // Act
        using var response = await client.GetAsync(JobUri(jobId), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Job_status_response_carries_only_state_fields()
    {
        // Arrange
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "Background jobs are PostgreSQL-only; the module is not registered on this provider.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        // A job that ran, reported progress and failed retryably, so every optional field holds a
        // value and the property set is the contract rather than an artifact of how nulls serialize.
        var jobId = await SeedJobAsync(
            world,
            world.SeedResult!.TenantIds[0],
            cancellationToken,
            job =>
            {
                var attemptAt = DateTime.UtcNow.AddSeconds(5);
                job.Claim(attemptAt);
                job.ReportProgress(40, "Streaming row 4,000 of 10,000");
                job.Reschedule(attemptAt.AddMinutes(1), "The job could not be completed.");
            });

        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);

        // Act
        using var response = await client.GetAsync(JobUri(jobId), cancellationToken);

        // Assert - the row carries state only, so nothing a handler produced can reach the caller here.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            new[] { "id", "type", "status", "progressPercentage", "statusMessage", "errorMessage" });
    }

    private static Uri JobUri(long jobId) => new($"/api/jobs/{jobId}", UriKind.Relative);

    /// <remarks>
    /// Written straight to the queue table: nothing enqueues jobs yet, and the read under test does
    /// not care how a row arrived.
    /// </remarks>
    private static async Task<long> SeedJobAsync(
        IntegrationTestWorld world,
        long tenantId,
        CancellationToken cancellationToken,
        Action<BackgroundJob>? prepare = null)
    {
        using var scope = world.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IJobsDbContext>();

        var job = new BackgroundJob("SubmissionExport", """{"formId":"1"}""", tenantId, DateTime.UtcNow);
        prepare?.Invoke(job);
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
