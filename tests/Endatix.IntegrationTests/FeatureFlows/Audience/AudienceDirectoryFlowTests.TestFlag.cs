using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Fixing the test flag after the fact: a submission a person's link opened, marked as test, frees the link for a
/// real one; a form's test submissions delete in one call.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Marking_a_link_opened_submission_as_test_lets_the_same_link_open_a_new_real_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Canton", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var testedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");

        // Act
        using var marked = await SetTestAsync(admin, link.FormId, testedId, isTest: true);
        var released = await WaitUntilLinkReleasedAsync(world.Services, link.FormId);
        var reopened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.True((await admin.ReadAsync(marked)).GetProperty("isTestSubmission").GetBoolean());
        Assert.True(released, "The submission.updated outbox handler should free the link.");
        Assert.True(reopened.GetProperty("created").GetBoolean());
        var realId = IdOf(reopened, "submissionId");
        Assert.NotEqual(testedId, realId);
        Assert.False((await ReadSubmissionAsync(admin, link.FormId, realId)).GetProperty("isTestSubmission").GetBoolean());
    }

    /// <summary>
    /// Marked as test and deleted in one save, so the release reads a deleted submission whenever the relay runs: the
    /// order a go-live clean-up produces when it beats the outbox.
    /// </summary>
    [Fact]
    public async Task A_link_whose_submission_was_marked_as_test_and_deleted_opens_a_new_real_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Xenia", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var testedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");

        // Act
        await MarkAsTestAndDeleteAsync(world.Services, testedId);
        var released = await WaitUntilLinkReleasedAsync(world.Services, link.FormId);
        var reopened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Assert
        Assert.True(released, "A deleted test submission should still free the link.");
        Assert.True(reopened.GetProperty("created").GetBoolean());
        Assert.NotEqual(testedId, IdOf(reopened, "submissionId"));
    }

    [Fact]
    public async Task Marking_a_test_submission_real_when_the_person_has_a_real_one_is_a_conflict_naming_it()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Lima", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var testedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");
        using var marked = await SetTestAsync(admin, link.FormId, testedId, isTest: true);
        Assert.True(await WaitUntilLinkReleasedAsync(world.Services, link.FormId));
        var realId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");

        // Act
        using var conflict = await SetTestAsync(admin, link.FormId, testedId, isTest: false);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await admin.ReadAsync(conflict);
        Assert.Equal("submission_exists", problem.GetProperty("errorCode").GetString());
        Assert.Equal(realId, IdOf(problem, "submissionId"));
        Assert.True((await ReadSubmissionAsync(admin, link.FormId, testedId)).GetProperty("isTestSubmission").GetBoolean());
    }

    [Fact]
    public async Task Deleting_test_submissions_removes_only_that_forms_tests_and_tells_reporting()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var otherFormId = await admin.CreateFormAsync();
        var testId = await TestSubmissionAsync(admin, formId);
        var realId = await OnBehalfIdAsync(admin, formId);
        var otherTestId = await TestSubmissionAsync(admin, otherFormId);

        // Act
        using var response = await admin.PostAsync($"/api/forms/{formId}/submissions/test/delete", new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await admin.ReadAsync(response)).GetProperty("deletedCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, await SubmissionStatusAsync(admin, formId, testId));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, realId));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, otherFormId, otherTestId));
        Assert.Equal([testId], await DeletedEventSubmissionIdsAsync(world.Services, [testId, realId, otherTestId]));
    }

    [Fact]
    public async Task A_submission_marked_as_test_leaves_the_default_export()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var testId = await CompletedWithAnswerAsync(admin, formId, "marked-as-test");
        await CompletedWithAnswerAsync(admin, formId, "stays-real");
        await ExpectOkAsync(admin.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));

        // Act
        using var marked = await SetTestAsync(admin, formId, testId, isTest: true);
        var csv = await ExportAsync(admin, formId, "csv");

        // Assert
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Contains("stays-real", csv);
        Assert.DoesNotContain("marked-as-test", csv);
    }

    /// <summary>
    /// The submission becomes a test with no outbox message at all, so only redeem can free the link: the person
    /// gets a new real submission, not the test one, however late the release handler runs.
    /// </summary>
    [Fact]
    public async Task Redeeming_a_link_whose_submission_became_a_test_opens_a_new_real_one_without_the_outbox()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Akron", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var testedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");
        await SetSubmissionColumnsSilentlyAsync(world.Services, testedId, isTest: true, isDeleted: false);

        // Act
        var reopened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Assert
        Assert.True(reopened.GetProperty("created").GetBoolean());
        var realId = IdOf(reopened, "submissionId");
        Assert.NotEqual(testedId, realId);
        Assert.Equal(realId, await LinkSubmissionIdAsync(world.Services, link.FormId));
        Assert.False((await ReadSubmissionAsync(admin, link.FormId, realId)).GetProperty("isTestSubmission").GetBoolean());
    }

    /// <summary>
    /// A deleted test submission the link still claims, as a go-live clean-up of a test created before the release,
    /// no longer leaves the link answering 404 for good.
    /// </summary>
    [Fact]
    public async Task Redeeming_a_link_whose_test_submission_was_deleted_opens_a_new_one()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Toledo");
        using var anonymous = world.AnonymousClient();
        var deletedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");
        await SetSubmissionColumnsSilentlyAsync(world.Services, deletedId, isTest: true, isDeleted: true);

        // Act
        var reopened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Assert
        Assert.True(reopened.GetProperty("created").GetBoolean());
        Assert.NotEqual(deletedId, IdOf(reopened, "submissionId"));
    }

    /// <summary>
    /// On a form without the one-response limit nothing keys the person by Submitter, but the link that opened the
    /// test submission has since opened a real one: marking the test real would give one link two real responses.
    /// </summary>
    [Fact]
    public async Task Marking_a_test_submission_real_when_its_link_opened_a_real_one_is_a_conflict_naming_it()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Dover");
        using var anonymous = world.AnonymousClient();
        var testedId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");
        using var marked = await SetTestAsync(admin, link.FormId, testedId, isTest: true);
        var realId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");

        // Act
        using var conflict = await SetTestAsync(admin, link.FormId, testedId, isTest: false);

        // Assert
        Assert.NotEqual(testedId, realId);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(realId, IdOf(await admin.ReadAsync(conflict), "submissionId"));
        Assert.True((await ReadSubmissionAsync(admin, link.FormId, testedId)).GetProperty("isTestSubmission").GetBoolean());
    }

    /// <summary>
    /// A schema replace clears a test submission's flattened row while the form has no real submissions. Marking it
    /// real flattens it again, so it is back in the default export without a backfill. This host keeps the relay
    /// closed, so the mark's <c>submission.updated</c> message is handed to the inline handlers here.
    /// </summary>
    [Fact]
    public async Task A_test_submission_marked_real_returns_to_the_default_export()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var testId = await CompletedWithAnswerAsync(admin, formId, "back-for-real");
        await CompletedWithAnswerAsync(admin, formId, "always-real");
        using var markedTest = await SetTestAsync(admin, formId, testId, isTest: true);
        await ExpectOkAsync(admin.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        await DeleteFlattenedRowAsync(world.Services, testId);

        // Act
        using var markedReal = await SetTestAsync(admin, formId, testId, isTest: false);
        await DeliverLatestUpdateAsync(world.Services, testId);
        var csv = await ExportAsync(admin, formId, "csv");

        // Assert
        Assert.Equal(HttpStatusCode.OK, markedReal.StatusCode);
        Assert.Contains("back-for-real", csv);
    }

    [Fact]
    public async Task A_test_opened_as_a_person_cannot_be_marked_real()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Quito", limitOnePerUser: true);
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var testId = IdOf(opened, "submissionId");

        // Act
        using var marked = await SetTestAsync(admin, person.FormId, testId, isTest: false);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, marked.StatusCode);
        Assert.True((await ReadSubmissionAsync(admin, person.FormId, testId)).GetProperty("isTestSubmission").GetBoolean());
    }

    private static Task<HttpResponseMessage> SetTestAsync(AudienceApi admin, long formId, long submissionId, bool isTest) =>
        admin.Client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/test", new { isTest }, admin.CancellationToken);

    private static async Task<long> OnBehalfIdAsync(AudienceApi admin, long formId)
    {
        using var created = await OnBehalfAsync(admin, formId, $"{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return IdOf(await admin.ReadAsync(created), "id");
    }

    private static async Task<long> TestSubmissionAsync(AudienceApi admin, long formId)
    {
        var submissionId = await OnBehalfIdAsync(admin, formId);
        using var marked = await SetTestAsync(admin, formId, submissionId, isTest: true);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return submissionId;
    }

    private static async Task<long> CompletedWithAnswerAsync(AudienceApi admin, long formId, string answer)
    {
        using var created = await admin.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new
            {
                isComplete = true,
                jsonData = JsonSerializer.Serialize(new { q1 = answer }),
                submitter = new { externalSubjectId = answer, displayId = answer, authProvider = "Integration" },
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return IdOf(await admin.ReadAsync(created), "id");
    }

    private static async Task<HttpStatusCode> SubmissionStatusAsync(AudienceApi admin, long formId, long submissionId)
    {
        using var response = await admin.Client.GetAsync($"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>Which of the submissions have a <c>submission.deleted</c> message, the one Reporting drops its row on.</summary>
    private static async Task<long[]> DeletedEventSubmissionIdsAsync(IServiceProvider services, long[] submissionIds)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payloads = await app.OutboxMessages.AsNoTracking()
            .Where(row => row.EventType == SubmissionDeletedEvent.EventTypeName)
            .Select(row => row.Payload)
            .ToListAsync(cancellationToken);
        var deleted = payloads.Select(payload => IdOf(JsonDocument.Parse(payload).RootElement, "submissionId")).ToHashSet();
        return submissionIds.Where(deleted.Contains).ToArray();
    }

    private static async Task MarkAsTestAndDeleteAsync(IServiceProvider services, long submissionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await app.Submissions.SingleAsync(item => item.Id == submissionId, cancellationToken);
        submission.MarkAsTest();
        submission.Delete();
        await app.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes what <see cref="Submission.MarkAsTest"/> (and a delete) would, straight to the row and raising no event,
    /// so no outbox handler follows. The one-per-person key goes with the flag, as it does in the mark.
    /// </summary>
    private static async Task SetSubmissionColumnsSilentlyAsync(IServiceProvider services, long submissionId, bool isTest, bool isDeleted)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Submissions
            .IgnoreQueryFilters()
            .Where(submission => submission.Id == submissionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(submission => submission.IsTestSubmission, isTest)
                    .SetProperty(submission => submission.RestrictionKey, (string?)null)
                    .SetProperty(submission => submission.IsDeleted, isDeleted),
                TestContext.Current.CancellationToken);
    }

    /// <summary>What a schema replace on a form without real submissions leaves: no flattened row for it.</summary>
    private static async Task DeleteFlattenedRowAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<IReportingDbContext>().FlattenedSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.SubmissionId == submissionId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, deleted);
    }

    /// <summary>What the relay does with the submission's latest <c>submission.updated</c> message.</summary>
    private static async Task DeliverLatestUpdateAsync(IServiceProvider services, long submissionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var messages = await app.OutboxMessages.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(row => row.EventType == SubmissionUpdatedEvent.EventTypeName)
            .OrderByDescending(row => row.Id)
            .ToListAsync(cancellationToken);
        var latest = messages.First(row =>
            IdOf(JsonDocument.Parse(row.Payload).RootElement, "submissionId") == submissionId);
        var handlers = scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>()
            .Where(handler => handler.EventTypes.Contains(SubmissionUpdatedEvent.EventTypeName));
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(new OutboxMessageView(latest), cancellationToken);
        }
    }

    /// <summary>Polls the form's one link until its claim is cleared; outbox handlers run in the background.</summary>
    private static async Task<bool> WaitUntilLinkReleasedAsync(IServiceProvider services, long formId)
    {
        var deadline = DateTime.UtcNow + OutboxWait;
        while (DateTime.UtcNow < deadline)
        {
            if (await LinkSubmissionIdAsync(services, formId) is null)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        return false;
    }
}
