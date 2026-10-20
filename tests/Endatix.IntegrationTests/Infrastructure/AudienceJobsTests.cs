using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Audience's outbox work delivered as background jobs: the host starts with the queue on and the module's own
/// inline handlers registered, and each event the API raises is done by Audience's job.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AudienceJobsTests(DbIntegrationFixture fixture)
{
    private const string DefinitionJson =
        """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Deleting_a_form_drops_its_audience_as_an_audience_job()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(ct);
        var seeded = await SeedSubmissionAsync(host, "audience-form-deletion", ct);
        var membershipId = await SeedPersonAsync(host, seeded, ct);

        // Act
        await DeleteFormAsync(host, seeded.FormId, ct);
        var job = await WaitForFinishedJobAsync(host, new ExpectedJob("AudienceDeleteFormAudience", seeded.TenantId), ct);

        // Assert — it succeeded on its first attempt.
        job.Should().Be((3, 1));
        (await IsMembershipDeletedAsync(host, membershipId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Marking_a_link_opened_submission_as_test_frees_the_link_as_an_audience_job()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(ct);
        var seeded = await SeedSubmissionAsync(host, "audience-test-mark", ct);
        var membershipId = await SeedPersonAsync(host, seeded, ct);
        var linkId = await SeedOpenedLinkAsync(host, new OpenedLink(seeded, membershipId), ct);

        // Act
        await MarkAsTestAsync(host, seeded.SubmissionId, ct);
        var job = await WaitForFinishedJobAsync(host, new ExpectedJob("AudienceReleaseLinkOnTestMark", seeded.TenantId), ct);

        // Assert — it succeeded on its first attempt.
        job.Should().Be((3, 1));
        (await LinkSubmissionIdAsync(host, linkId, ct)).Should().BeNull();
    }

    [Fact]
    public async Task Releasing_a_link_waits_for_an_uncommitted_mark_as_real_and_then_keeps_the_claim()
    {
        // Arrange — a test submission whose claim is written after its own release job has run, and a mark as real
        // that has updated the submission's row but not committed.
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartHostAsync(ct);
        var seeded = await SeedSubmissionAsync(host, "audience-release-race", ct);
        var membershipId = await SeedPersonAsync(host, seeded, ct);
        await MarkAsTestAsync(host, seeded.SubmissionId, ct);
        (await WaitForFinishedJobAsync(host, new ExpectedJob("AudienceReleaseLinkOnTestMark", seeded.TenantId), ct))
            .Should().Be((3, 1));
        var linkId = await SeedOpenedLinkAsync(host, new OpenedLink(seeded, membershipId), ct);
        using var markScope = host.Services.CreateScope();
        var markDb = markScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var markAsReal = await markDb.Database.BeginTransactionAsync(ct);
        var submission = await markDb.Submissions.SingleAsync(item => item.Id == seeded.SubmissionId, ct);
        submission.MarkAsReal(enforceSingleSubmissionGate: false);
        await markDb.SaveChangesAsync(ct);

        // Act — the release starts while the mark holds the row; then the mark commits.
        using var releaseScope = host.Services.CreateScope();
        var release = releaseScope.ServiceProvider.GetRequiredService<ReleaseLinkOnTestMarkOutboxHandler>().ProcessAsync(
            new ReleaseLinkOnTestMarkOutboxHandler.MarkedSubmission(seeded.TenantId, seeded.SubmissionId), ct);
        var releasedBeforeTheMark = await Task.WhenAny(release, Task.Delay(TimeSpan.FromSeconds(2), ct)) == release;
        await markAsReal.CommitAsync(ct);
        await release;

        // Assert — the release waited for the mark, then found a real submission and left its claim alone.
        releasedBeforeTheMark.Should().BeFalse();
        (await LinkSubmissionIdAsync(host, linkId, ct)).Should().Be(seeded.SubmissionId);
    }

    // The modules' inline handlers stay registered, so the host only starts when each has its job subscription.
    private Task<FanOutHost> StartHostAsync(CancellationToken ct) =>
        FanOutHost.StartAsync(
            fixture, new FanOutHostSetup(DeliverToJobQueue: true, _ => { }) { KeepsInlineHandlers = true }, ct);

    private static async Task<SeededSubmission> SeedSubmissionAsync(FanOutHost host, string tenantName, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant(tenantName, ShortUrlFor(tenantName));
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(ct);
        var form = Form.Create(new FormCreateArgs(TenantId: tenant.Id, Name: "audience form"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        var definition = new FormDefinition(tenant.Id, isDraft: false, jsonData: DefinitionJson);
        form.AddFormDefinition(definition, isActive: false);
        await db.SaveChangesAsync(ct);
        form.SetActiveFormDefinition(definition);
        await db.SaveChangesAsync(ct);
        var submission = Submission.Create(new SubmissionCreateArgs(tenant.Id, form.Id, definition.Id, """{"q1":"hello"}"""));
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);
        return new SeededSubmission(tenant.Id, form.Id, submission.Id);
    }

    private static string ShortUrlFor(string name)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        return new string([.. hash.Take(Endatix.Core.Common.ShortUrl.StandardLength)
            .Select(value => Endatix.Core.Common.ShortUrl.Alphabet[value % Endatix.Core.Common.ShortUrl.Alphabet.Length])]);
    }

    /// <summary>A person on the form, with one property; returns the membership.</summary>
    private static async Task<long> SeedPersonAsync(FanOutHost host, SeededSubmission seeded, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var normalizer = scope.ServiceProvider.GetRequiredService<IValueNormalizer>();
        audience.Properties.Add(new Property(new PropertyCreateArgs(
            seeded.TenantId, seeded.FormId, "City", AudienceDataTypeCodes.Text, SortOrder: 0)));
        var member = new Member(new MemberCreateArgs(
            seeded.TenantId, "ada@example.com", AudienceIdentifierKindCodes.Email, normalizer));
        audience.Members.Add(member);
        await audience.SaveChangesAsync(ct);
        var membership = new Membership(seeded.TenantId, seeded.FormId, member.Id);
        audience.Memberships.Add(membership);
        await audience.SaveChangesAsync(ct);
        return membership.Id;
    }

    // The claim is written straight to the row, as redeem leaves it, so no link has to be opened over HTTP.
    private static async Task<long> SeedOpenedLinkAsync(FanOutHost host, OpenedLink opened, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var (link, _) = AudienceLink.Issue(opened.Submission.TenantId, opened.Submission.FormId, opened.MembershipId);
        audience.Links.Add(link);
        await audience.SaveChangesAsync(ct);
        await host.Database.ExecuteAsync(
            $"""UPDATE audience."Links" SET "SubmissionId" = {opened.Submission.SubmissionId}, "OpenedAt" = @now WHERE "Id" = {link.Id}""",
            new Dictionary<string, object> { ["@now"] = DateTime.UtcNow },
            ct);
        return link.Id;
    }

    private static async Task DeleteFormAsync(FanOutHost host, long formId, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var form = await db.Forms
            .Include(form => form.FormDefinitions)
            .ThenInclude(definition => definition.Submissions)
            .SingleAsync(form => form.Id == formId, ct);
        form.Delete();
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkAsTestAsync(FanOutHost host, long submissionId, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await db.Submissions.SingleAsync(item => item.Id == submissionId, ct);
        submission.MarkAsTest();
        await db.SaveChangesAsync(ct);
    }

    private static async Task<bool> IsMembershipDeletedAsync(FanOutHost host, long membershipId, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await audience.Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Select(membership => membership.IsDeleted)
            .SingleAsync(ct);
    }

    private static async Task<long?> LinkSubmissionIdAsync(FanOutHost host, long linkId, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await audience.Links.IgnoreQueryFilters().AsNoTracking()
            .Where(link => link.Id == linkId)
            .Select(link => link.SubmissionId)
            .SingleAsync(ct);
    }

    // Waits until the job has finished, whichever way, so a failed job fails the test at once with its status.
    private static async Task<(int Status, int AttemptCount)?> WaitForFinishedJobAsync(
        FanOutHost host,
        ExpectedJob expected,
        CancellationToken ct)
    {
        (int Status, int AttemptCount)? job = null;
        await JobsTestWait.UntilAsync(
            async () => (job = await JobAsync(host, expected, ct)) is { Status: 3 or 4 or 5 },
            Patience,
            ct);
        return job;
    }

    private static async Task<(int Status, int AttemptCount)?> JobAsync(FanOutHost host, ExpectedJob expected, CancellationToken ct)
    {
        var rows = await host.Database.QueryAsync(
            $"""SELECT "Status", "AttemptCount" FROM jobs."BackgroundJobs" WHERE "JobType" = '{expected.JobType}' AND "TenantId" = {expected.TenantId}""",
            reader => (reader.GetInt32(0), reader.GetInt32(1)),
            ct);
        return rows.Count == 0 ? null : rows[0];
    }

    private sealed record SeededSubmission(long TenantId, long FormId, long SubmissionId);

    private sealed record OpenedLink(SeededSubmission Submission, long MembershipId);

    /// <summary>The job, by its type and tenant, waited for until it finishes.</summary>
    private sealed record ExpectedJob(string JobType, long TenantId);
}
