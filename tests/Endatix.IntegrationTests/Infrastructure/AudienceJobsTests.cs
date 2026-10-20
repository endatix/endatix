using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Audience's outbox work delivered through the job queue (<c>Endatix:Outbox:DeliverToJobQueue</c>): each message
/// becomes an Audience job that reads the message and runs the inline handler's work.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AudienceJobsTests(DbIntegrationFixture fixture)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_test_mark_runs_the_link_release_as_its_own_job()
    {
        const long messageId = 910;
        const string changeKind = "test";
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await FanOutHost.StartAsync(
            fixture, new FanOutHostSetup(DeliverToJobQueue: true, _ => { }), ct);
        var (tenantId, formId, submissionId) = await SeedTestSubmissionAsync(host, $"audience-jobs-{messageId}", ct);

        // Act
        await host.InsertMessageAsync(
            new OutboxRow(messageId, "submission.updated", tenantId,
                $$"""{"formId":"{{formId}}","submissionId":"{{submissionId}}","tenantId":"{{tenantId}}","changeKind":"{{changeKind}}"}"""),
            ct);
        var completed = await JobsTestWait.UntilAsync(
            async () => await host.Database.CountAsync(
                $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "DedupKey" = '{messageId}:AudienceReleaseLinkOnTestMark' AND "Status" = 3""",
                ct) == 1,
            Patience,
            ct);

        // Assert
        completed.Should().BeTrue();
    }

    private static async Task<(long TenantId, long FormId, long SubmissionId)> SeedTestSubmissionAsync(
        FanOutHost host,
        string tenantName,
        CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tenantName));
        var shortUrl = new string([.. hash.Take(Endatix.Core.Common.ShortUrl.StandardLength)
            .Select(value => Endatix.Core.Common.ShortUrl.Alphabet[value % Endatix.Core.Common.ShortUrl.Alphabet.Length])]);
        var tenant = new Tenant(tenantName, shortUrl);
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(ct);
        var form = Form.Create(new FormCreateArgs(TenantId: tenant.Id, Name: "audience form"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        var definition = new FormDefinition(tenant.Id, isDraft: false, jsonData: "{}");
        form.AddFormDefinition(definition, isActive: true);
        await db.SaveChangesAsync(ct);
        var submission = Submission.Create(new SubmissionCreateArgs(
            tenant.Id, form.Id, definition.Id, "{}", IsTestSubmission: true));
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);
        return (tenant.Id, form.Id, submission.Id);
    }
}
