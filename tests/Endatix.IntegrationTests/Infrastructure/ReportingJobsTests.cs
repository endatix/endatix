using System.Net;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Reporting's outbox work delivered as background jobs, each its own job with its own retries.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ReportingJobsTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const string DefinitionJson =
        """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Flatten_runs_as_its_own_job()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: true, ct);
        var (tenantId, formId, submissionId) = await SeedSubmissionAsync(host, "reporting-flatten", ct);
        await WaitForSeedingJobsAsync(host, ct);

        // Act
        await host.InsertMessageAsync(900, "submission.completed", tenantId, ct, SubmissionPayload(tenantId, formId, submissionId));
        var completed = await WaitForJobAsync(host, "900:ReportingFlattenSubmission", status => status == 3, ct);

        // Assert
        completed.Should().BeTrue();
        (await host.Database.CountAsync(
            """SELECT count(*) FROM jobs."BackgroundJobs" WHERE "DedupKey" = '900:ReportingFlattenSubmission'""", ct))
            .Should().Be(1);
        (await host.Database.CountAsync(
            $"""SELECT count(*) FROM reporting."FlattenedSubmissions" WHERE "SubmissionId" = {submissionId}""", ct))
            .Should().Be(1);
    }

    [Fact]
    public async Task Failing_webhook_does_not_retry_flatten()
    {
        // Arrange — the tenant's webhook endpoint answers 503 to the same message.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var receiver = new StubWebHookReceiver();
        await using var host = await StartHostAsync(reporting: true, ct, new Dictionary<string, string?>
        {
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffBaseSeconds"] = "1",
            ["Endatix:BackgroundJobs:JobTypes:WebHookDelivery:BackoffCapSeconds"] = "1",
        });
        var (tenantId, formId, submissionId) = await SeedSubmissionAsync(host, "reporting-failing-webhook", ct);
        await ConfigureTenantWebHookAsync(host, tenantId, receiver.UrlFor("down", HttpStatusCode.ServiceUnavailable), ct);
        await WaitForSeedingJobsAsync(host, ct);

        // Act
        await host.InsertMessageAsync(901, "submission.completed", tenantId, ct, SubmissionPayload(tenantId, formId, submissionId));
        await WaitForJobAsync(host, "901:ReportingFlattenSubmission", status => status == 3, ct);
        await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync(
                """SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" = 'WebHookDelivery' AND "DedupKey" LIKE '901:%' AND "AttemptCount" > 1""",
                ct) == 1,
            Patience,
            ct);

        // Assert
        var flatten = await JobAsync(host, "901:ReportingFlattenSubmission", ct);
        flatten.Should().Be((3, 1));
        var webhook = await host.Database.QueryAsync(
            """SELECT "Status", "AttemptCount" FROM jobs."BackgroundJobs" WHERE "JobType" = 'WebHookDelivery' AND "DedupKey" LIKE '901:%'""",
            reader => (reader.GetInt32(0), reader.GetInt32(1)),
            ct);
        webhook.Should().ContainSingle().Which.Should().Match<(int Status, int AttemptCount)>(
            job => job.AttemptCount > 1 && job.Status != 3);
    }

    [Fact]
    public async Task Tenant_created_seeds_formats_as_new_tenant_job()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: true, ct);

        // Act — the tenant is created without seeding anything itself, so only the job can seed its formats.
        var tenantId = await CreateTenantAsync(host, "reporting-new-tenant", ct);
        var completed = await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" = 'ReportingSeedDefaultExportFormats' AND "TenantId" = {tenantId} AND "Status" = 3""",
                ct) == 1,
            Patience,
            ct);

        // Assert
        completed.Should().BeTrue();
        (await host.Database.CountAsync(
            $"""SELECT count(*) FROM reporting."ExportFormats" WHERE "TenantId" = {tenantId}""", ct))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reporting_off_creates_no_reporting_jobs()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: false, ct);

        // Act
        await host.InsertMessageAsync(902, "form.deleted", 5, ct, """{"formId":"12","tenantId":"5"}""");
        await JobsTestWait.UntilAsync(async () => (await host.ReadMessageAsync(902, ct)).Status == 1, Patience, ct);

        // Assert
        (await host.Database.CountAsync(
            """SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" LIKE 'Reporting%'""", ct))
            .Should().Be(0);
    }

    private Task<FanOutHost> StartHostAsync(bool reporting, CancellationToken ct, Dictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>(settings ?? []) { ["Endatix:FeatureFlags:ReportingModule"] = reporting.ToString() };
        return FanOutHost.StartAsync(fixture.ConnectionString, deliverToJobQueue: true, _ => { }, ct, settings: all);
    }

    private static async Task<long> CreateTenantAsync(FanOutHost host, string name, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        var shortUrl = new string([.. hash.Take(Endatix.Core.Common.ShortUrl.StandardLength)
            .Select(value => Endatix.Core.Common.ShortUrl.Alphabet[value % Endatix.Core.Common.ShortUrl.Alphabet.Length])]);
        var tenant = new Tenant(name, shortUrl);
        tenant.RaiseCreated();
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant.Id;
    }

    private static async Task<(long TenantId, long FormId, long SubmissionId)> SeedSubmissionAsync(
        FanOutHost host,
        string tenantName,
        CancellationToken ct)
    {
        var tenantId = await CreateTenantAsync(host, tenantName, ct);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "reporting form"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        var definition = new FormDefinition(tenantId, isDraft: false, jsonData: DefinitionJson);
        form.AddFormDefinition(definition, isActive: false);
        await db.SaveChangesAsync(ct);
        form.SetActiveFormDefinition(definition);
        await db.SaveChangesAsync(ct);
        var submission = new Submission(tenantId, """{"q1":"hello"}""", form.Id, definition.Id, isComplete: true);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);
        return (tenantId, form.Id, submission.Id);
    }

    private static async Task ConfigureTenantWebHookAsync(FanOutHost host, long tenantId, string url, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = url }] },
            },
        };
        var settings = db.Set<TenantSettings>().FirstOrDefault(row => row.TenantId == tenantId);
        if (settings is null)
        {
            db.Set<TenantSettings>().Add(new TenantSettings(tenantId, webHookSettingsJson: JsonSerializer.Serialize(config)));
        }
        else
        {
            settings.UpdateWebHookSettings(config);
        }

        await db.SaveChangesAsync(ct);
    }

    // Seeding raises events of its own — the form's definition, the submission — whose Reporting jobs write the
    // same schema and flattened row. Letting them finish first keeps them from racing the job under test.
    private static Task WaitForSeedingJobsAsync(FanOutHost host, CancellationToken ct) =>
        JobsTestWait.UntilAsync(
            async () =>
                await host.Database.CountAsync("""SELECT count(*) FROM "OutboxMessages" WHERE "Status" = 0""", ct) == 0
                && await host.Database.CountAsync(
                    """SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" LIKE 'Reporting%' AND "Status" IN (0, 1, 2)""",
                    ct) == 0,
            Patience,
            ct);

    private static string SubmissionPayload(long tenantId, long formId, long submissionId) =>
        $$"""{"formId":"{{formId}}","submissionId":"{{submissionId}}","tenantId":"{{tenantId}}"}""";

    private static Task<bool> WaitForJobAsync(FanOutHost host, string dedupKey, Func<int, bool> reached, CancellationToken ct) =>
        JobsTestWait.UntilAsync(
            async () => await JobAsync(host, dedupKey, ct) is { } job && reached(job.Status),
            Patience,
            ct);

    private static async Task<(int Status, int AttemptCount)?> JobAsync(FanOutHost host, string dedupKey, CancellationToken ct)
    {
        var rows = await host.Database.QueryAsync(
            $"""SELECT "Status", "AttemptCount" FROM jobs."BackgroundJobs" WHERE "DedupKey" = '{dedupKey}'""",
            reader => (reader.GetInt32(0), reader.GetInt32(1)),
            ct);
        return rows.Count == 0 ? null : rows[0];
    }
}
