using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Events;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
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
