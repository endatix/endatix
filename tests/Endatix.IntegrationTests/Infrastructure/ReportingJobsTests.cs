using System.Net;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
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
        await host.InsertMessageAsync(new OutboxRow(900, "submission.completed", tenantId, SubmissionPayload(tenantId, formId, submissionId)), ct);
        var completed = await WaitForJobAsync(host, new ExpectedJob("900:ReportingFlattenSubmission", status => status == 3), ct);

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
        await ConfigureTenantWebHookAsync(
            host, new TenantWebHook(tenantId, receiver.UrlFor("down", HttpStatusCode.ServiceUnavailable)), ct);
        await WaitForSeedingJobsAsync(host, ct);

        // Act
        await host.InsertMessageAsync(new OutboxRow(901, "submission.completed", tenantId, SubmissionPayload(tenantId, formId, submissionId)), ct);
        await WaitForJobAsync(host, new ExpectedJob("901:ReportingFlattenSubmission", status => status == 3), ct);
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
        await host.InsertMessageAsync(new OutboxRow(902, "form.deleted", 5, """{"formId":"12","tenantId":"5"}"""), ct);
        await JobsTestWait.UntilAsync(async () => (await host.ReadMessageAsync(902, ct)).Status == 1, Patience, ct);

        // Assert
        (await host.Database.CountAsync(
            """SELECT count(*) FROM jobs."BackgroundJobs" WHERE "JobType" LIKE 'Reporting%'""", ct))
            .Should().Be(0);
    }

    [Fact]
    public async Task Flatten_of_a_submission_deleted_before_it_ran_completes_and_leaves_no_row()
    {
        // Arrange — the submission was flattened once, then deleted without its own cleanup having run yet.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: true, ct);
        var (tenantId, formId, submissionId) = await SeedSubmissionAsync(host, "reporting-deleted-before-flatten", ct);
        await WaitForSeedingJobsAsync(host, ct);
        await host.Database.ExecuteAsync(
            $"""UPDATE "Submissions" SET "IsDeleted" = true, "DeletedAt" = now() WHERE "Id" = {submissionId}""", ct);

        // Act — a flatten of that submission, such as a retry, runs after the deletion.
        await host.InsertMessageAsync(new OutboxRow(910, "submission.completed", tenantId, SubmissionPayload(tenantId, formId, submissionId)), ct);
        var finished = await WaitForJobAsync(host, new ExpectedJob("910:ReportingFlattenSubmission", status => status is 3 or 4 or 5), ct);

        // Assert — it succeeds on its first attempt instead of retrying, and no row is left for the deleted submission.
        finished.Should().BeTrue();
        (await JobAsync(host, "910:ReportingFlattenSubmission", ct)).Should().Be((3, 1));
        (await host.Database.CountAsync(
            $"""SELECT count(*) FROM reporting."FlattenedSubmissions" WHERE "SubmissionId" = {submissionId}""", ct))
            .Should().Be(0);
    }

    [Fact]
    public async Task Flatten_of_an_older_revision_finishing_last_leaves_the_newer_data()
    {
        // Arrange — the processor is driven directly with the submission at revision 5, then at revision 3.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: true, ct);
        var (tenantId, formId, submissionId) = await SeedSubmissionAsync(host, "reporting-stale-flatten", ct);
        await WaitForSeedingJobsAsync(host, ct);
        await SetSubmissionAsync(host, new SubmissionAnswer(submissionId, Revision: 5, Answer: "newer"), ct);
        await FlattenAsync(host, new SeededSubmission(tenantId, formId, submissionId), ct);
        await SetSubmissionAsync(host, new SubmissionAnswer(submissionId, Revision: 3, Answer: "older"), ct);

        // Act — the flatten that read revision 3 writes after the one that read revision 5.
        await FlattenAsync(host, new SeededSubmission(tenantId, formId, submissionId), ct);

        // Assert
        var rows = await host.Database.QueryAsync(
            $"""SELECT "DataJson"::text, "SourceRevision", "IntegrationStatus" FROM reporting."FlattenedSubmissions" WHERE "SubmissionId" = {submissionId}""",
            reader => (Data: reader.GetString(0), Revision: reader.GetInt64(1), Status: reader.GetString(2)),
            ct);
        rows.Should().ContainSingle();
        rows[0].Data.Should().Contain("newer").And.NotContain("older");
        rows[0].Revision.Should().Be(5);
        rows[0].Status.Should().Be("processed");
    }

    [Fact]
    public async Task Flatten_of_a_newer_revision_replaces_the_older_data()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(reporting: true, ct);
        var (tenantId, formId, submissionId) = await SeedSubmissionAsync(host, "reporting-newer-flatten", ct);
        await WaitForSeedingJobsAsync(host, ct);
        await SetSubmissionAsync(host, new SubmissionAnswer(submissionId, Revision: 3, Answer: "older"), ct);
        await FlattenAsync(host, new SeededSubmission(tenantId, formId, submissionId), ct);
        await SetSubmissionAsync(host, new SubmissionAnswer(submissionId, Revision: 5, Answer: "newer"), ct);

        // Act
        await FlattenAsync(host, new SeededSubmission(tenantId, formId, submissionId), ct);

        // Assert
        var rows = await host.Database.QueryAsync(
            $"""SELECT "DataJson"::text, "SourceRevision" FROM reporting."FlattenedSubmissions" WHERE "SubmissionId" = {submissionId}""",
            reader => (Data: reader.GetString(0), Revision: reader.GetInt64(1)),
            ct);
        rows.Should().ContainSingle();
        rows[0].Data.Should().Contain("newer");
        rows[0].Revision.Should().Be(5);
    }

    // Writes the submission's answer and revision straight to its row, so no event fans out a flatten of its own.
    private static Task SetSubmissionAsync(FanOutHost host, SubmissionAnswer answer, CancellationToken ct) =>
        host.Database.ExecuteAsync(
            $$$"""UPDATE "Submissions" SET "JsonData" = '{"q1":"{{{answer.Answer}}}"}', "Revision" = {{{answer.Revision}}} WHERE "Id" = {{{answer.SubmissionId}}}""",
            ct);

    private static async Task FlattenAsync(FanOutHost host, SeededSubmission seeded, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISubmissionFlatteningProcessor>()
            .ProcessAsync(seeded.TenantId, seeded.FormId, seeded.SubmissionId, ct);
    }

    private Task<FanOutHost> StartHostAsync(bool reporting, CancellationToken ct, Dictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>(settings ?? []) { ["Endatix:FeatureFlags:ReportingModule"] = reporting.ToString() };
        return FanOutHost.StartAsync(
            fixture.ConnectionString, new FanOutHostSetup(DeliverToJobQueue: true, _ => { }) { Settings = all }, ct);
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

    private static async Task<SeededSubmission> SeedSubmissionAsync(
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
        return new SeededSubmission(tenantId, form.Id, submission.Id);
    }

    private static async Task ConfigureTenantWebHookAsync(FanOutHost host, TenantWebHook webHook, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = webHook.Url }] },
            },
        };
        var settings = db.Set<TenantSettings>().FirstOrDefault(row => row.TenantId == webHook.TenantId);
        if (settings is null)
        {
            db.Set<TenantSettings>().Add(new TenantSettings(webHook.TenantId, webHookSettingsJson: JsonSerializer.Serialize(config)));
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

    private static Task<bool> WaitForJobAsync(FanOutHost host, ExpectedJob expected, CancellationToken ct) =>
        JobsTestWait.UntilAsync(
            async () => await JobAsync(host, expected.DedupKey, ct) is { } job && expected.Reached(job.Status),
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

    private sealed record SeededSubmission(long TenantId, long FormId, long SubmissionId);

    /// <summary>The answer and revision a test writes straight to a submission's row.</summary>
    private sealed record SubmissionAnswer(long SubmissionId, long Revision, string Answer);

    private sealed record TenantWebHook(long TenantId, string Url);

    /// <summary>The job, by its dedup key, and the status it is waited for.</summary>
    private sealed record ExpectedJob(string DedupKey, Func<int, bool> Reached);
}
