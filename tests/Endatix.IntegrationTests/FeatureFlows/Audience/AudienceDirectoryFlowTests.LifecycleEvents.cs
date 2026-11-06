using System.Net;
using System.Net.Http.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience's create entry points, a personalised link's first open and a test submission, each capture one
/// <c>submission.created</c> outbox row.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Redeeming_a_link_captures_one_created_row_and_a_reopen_none()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);
        await RedeemAsync(anonymous, formId, token);

        // Assert
        var rows = await SubmissionOutboxRows.ReadAsync(
            world.Services, IdOf(opened, "submissionId"), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(SubmissionLifecycleEvent.EventTypes.Created, row.EventType);
        Assert.Equal(CollectionStatusCodes.Viewed, row.CollectionStatus);
        Assert.False(row.IsTestSubmission);
    }

    [Fact]
    public async Task Opening_a_test_submission_captures_one_created_row_flagged_as_test()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");

        // Act
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));

        // Assert
        var rows = await SubmissionOutboxRows.ReadAsync(
            world.Services, IdOf(opened, "submissionId"), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(SubmissionLifecycleEvent.EventTypes.Created, row.EventType);
        Assert.Equal(CollectionStatusCodes.Viewed, row.CollectionStatus);
        Assert.True(row.IsTestSubmission);
    }

    [Fact]
    public async Task The_first_answer_after_a_link_open_captures_started_and_keeps_the_open_time()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        var submissionId = IdOf(opened, "submissionId");
        var openedAt = (await ReadSubmissionAsync(admin, formId, submissionId)).GetProperty("startedAt").GetDateTime();

        // Act
        using var saved = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, SubmissionAccessTokenOf(opened)),
            new { jsonData = """{"q1":"a"}""", isComplete = false },
            admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var rows = await SubmissionOutboxRows.ReadAsync(world.Services, submissionId, TestContext.Current.CancellationToken);
        Assert.Equal(
            [
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Created, CollectionStatusCodes.Viewed, 2, false),
                new SubmissionOutboxRow(SubmissionLifecycleEvent.EventTypes.Started, CollectionStatusCodes.InProgress, 3, false),
            ],
            rows);
        var read = await ReadSubmissionAsync(admin, formId, submissionId);
        Assert.Equal(openedAt, read.GetProperty("startedAt").GetDateTime());
    }
}
