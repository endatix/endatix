using System.Net;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Removing test participants over HTTP (<c>POST people/test/removal</c>): the people one at a time, then the test
/// submissions their Snapshot names. Real participants and their submissions are never touched.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Removing_test_participants_deletes_their_test_submissions_and_tells_reporting()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var real = await AddParticipantAsync(admin, formId, "real@example.com");
        var chosen = await AddTestParticipantAsync(admin, formId, "chosen@example.com");
        var kept = await AddTestParticipantAsync(admin, formId, "kept@example.com");
        long[] chosenTests =
        [
            await TestSubmissionIdAsync(admin, formId, chosen),
            await TestSubmissionIdAsync(admin, formId, chosen),
        ];
        var keptTest = await TestSubmissionIdAsync(admin, formId, kept);
        var realTest = await TestSubmissionIdAsync(admin, formId, real);
        var realSubmission = await OnBehalfIdAsync(admin, formId);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([chosen.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemovedTestPeople(1, 2, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(
            ["kept@example.com", "real@example.com"],
            Identifiers(await ListPeopleAsync(admin, formId, string.Empty)).Order());
        Assert.Equal(
            [HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK],
            await SubmissionStatusesAsync(admin, formId, [.. chosenTests, keptTest, realTest, realSubmission]));
        Assert.Equal(
            chosenTests,
            await DeletedEventSubmissionIdsAsync(world.Services, [.. chosenTests, keptTest, realTest, realSubmission]));
    }

    [Fact]
    public async Task Removing_every_test_participant_leaves_the_real_ones()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "real@example.com");
        var first = await AddTestParticipantAsync(admin, formId, "first@example.com");
        await AddTestParticipantAsync(admin, formId, "second@example.com");
        var firstTest = await TestSubmissionIdAsync(admin, formId, first);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval(null));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemovedTestPeople(2, 1, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(["real@example.com"], Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
        Assert.Equal(HttpStatusCode.NotFound, await SubmissionStatusAsync(admin, formId, firstTest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_a_real_participant_this_way_is_rejected_and_removes_nobody(bool dryRun)
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var real = await AddParticipantAsync(admin, formId, "real@example.com");
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var testerTest = await TestSubmissionIdAsync(admin, formId, tester);

        // Act
        using var response = await RemoveTestPeopleAsync(
            admin, formId, new TestRemoval([tester.MembershipId, real.MembershipId], DryRun: dryRun));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ["real@example.com", "tester@example.com"],
            Identifiers(await ListPeopleAsync(admin, formId, string.Empty)).Order());
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, testerTest));
    }

    [Fact]
    public async Task Removing_a_membership_that_is_not_on_the_form_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var otherFormId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var elsewhere = await AddTestParticipantAsync(admin, otherFormId, "elsewhere@example.com");

        // Act
        using var response = await RemoveTestPeopleAsync(
            admin, formId, new TestRemoval([tester.MembershipId, elsewhere.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["tester@example.com"], Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
    }

    [Fact]
    public async Task A_removed_test_participants_test_link_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var token = await AccessTokenAsync(admin, formId, await TestSubmissionIdAsync(admin, formId, tester));
        using var anonymous = world.AnonymousClient();
        using var before = await anonymous.GetAsync(ByAccessTokenUrl(formId, token), admin.CancellationToken);

        // Act
        using var removed = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var after = await anonymous.GetAsync(ByAccessTokenUrl(formId, token), admin.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Removing_test_participants_without_their_test_submissions_keeps_them()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var testerTest = await TestSubmissionIdAsync(admin, formId, tester);

        // Act
        using var response = await RemoveTestPeopleAsync(
            admin, formId, new TestRemoval([tester.MembershipId], AlsoDeleteTestSubmissions: false));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemovedTestPeople(1, 0, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Empty(Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, testerTest));
    }

    [Fact]
    public async Task A_dry_run_counts_what_would_be_removed_and_writes_nothing()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "real@example.com");
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        long[] testerTests =
        [
            await TestSubmissionIdAsync(admin, formId, tester),
            await TestSubmissionIdAsync(admin, formId, tester),
        ];

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval(null, DryRun: true));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemovedTestPeople(1, 2, true), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(
            ["real@example.com", "tester@example.com"],
            Identifiers(await ListPeopleAsync(admin, formId, string.Empty)).Order());
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK],
            await SubmissionStatusesAsync(admin, formId, testerTests));
    }

    [Fact]
    public async Task A_tester_on_two_forms_keeps_their_test_submissions_on_the_other_form()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var otherFormId = await admin.CreateFormAsync();
        var here = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var there = await AddTestParticipantAsync(admin, otherFormId, "tester@example.com");
        var hereTest = await TestSubmissionIdAsync(admin, formId, here);
        var thereTest = await TestSubmissionIdAsync(admin, otherFormId, there);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([here.MembershipId]));

        // Assert
        Assert.Equal(here.MemberId, there.MemberId);
        Assert.Equal(new RemovedTestPeople(1, 1, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(HttpStatusCode.NotFound, await SubmissionStatusAsync(admin, formId, hereTest));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, otherFormId, thereTest));
        Assert.Equal(["tester@example.com"], Identifiers(await ListPeopleAsync(admin, otherFormId, string.Empty)));
    }

    [Fact]
    public async Task A_member_back_on_the_form_as_a_real_participant_keeps_their_old_test_submissions()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "back@example.com");
        var oldTest = await TestSubmissionIdAsync(admin, formId, tester);
        using var removed = await RemoveTestPeopleAsync(
            admin, formId, new TestRemoval([tester.MembershipId], AlsoDeleteTestSubmissions: false));
        var real = await AddParticipantAsync(admin, formId, "back@example.com");

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval(null));

        // Assert
        Assert.Equal(tester.MemberId, real.MemberId);
        Assert.Equal(new RemovedTestPeople(0, 0, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, oldTest));
    }

    [Fact]
    public async Task A_former_real_participant_back_as_a_tester_loses_their_test_submissions()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var formerlyReal = await AddParticipantAsync(admin, formId, "back@example.com");
        using var left = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{formerlyReal.MembershipId}", admin.CancellationToken);
        var tester = await AddTestParticipantAsync(admin, formId, "back@example.com");
        var testerTest = await TestSubmissionIdAsync(admin, formId, tester);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, left.StatusCode);
        Assert.Equal(new RemovedTestPeople(1, 1, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(HttpStatusCode.NotFound, await SubmissionStatusAsync(admin, formId, testerTest));
    }

    [Fact]
    public async Task Removing_an_old_test_membership_keeps_the_test_submissions_of_the_members_current_one()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var old = await AddTestParticipantAsync(admin, formId, "again@example.com");
        using var first = await RemoveTestPeopleAsync(
            admin, formId, new TestRemoval([old.MembershipId], AlsoDeleteTestSubmissions: false));
        var current = await AddTestParticipantAsync(admin, formId, "again@example.com");
        var currentTest = await TestSubmissionIdAsync(admin, formId, current);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([old.MembershipId]));

        // Assert
        Assert.Equal(new RemovedTestPeople(0, 0, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(HttpStatusCode.OK, await SubmissionStatusAsync(admin, formId, currentTest));
        Assert.Equal(["again@example.com"], Identifiers(await ListPeopleAsync(admin, formId, string.Empty)));
    }

    [Fact]
    public async Task A_test_participants_on_behalf_submission_marked_as_test_is_kept()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        var tester = await AddTestParticipantAsync(admin, formId, "tester@example.com");
        var onBehalf = await OnBehalfOfAsync(admin, formId, tester.Identifier);
        using var marked = await SetTestAsync(admin, formId, onBehalf, isTest: true);
        var opened = await TestSubmissionIdAsync(admin, formId, tester);

        // Act
        using var response = await RemoveTestPeopleAsync(admin, formId, new TestRemoval([tester.MembershipId]));

        // Assert
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal(new RemovedTestPeople(1, 1, false), RemovedOf(await admin.ReadAsync(response)));
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.NotFound],
            await SubmissionStatusesAsync(admin, formId, [onBehalf, opened]));
    }

    private sealed record RemovedTestPeople(int People, int TestSubmissions, bool DryRun);

    /// <summary>
    /// A removal request: the participants (null for every test participant), by default with their test submissions,
    /// for real.
    /// </summary>
    private sealed record TestRemoval(long[]? MembershipIds, bool AlsoDeleteTestSubmissions = true, bool DryRun = false);

    private static Task<HttpResponseMessage> RemoveTestPeopleAsync(AudienceApi api, long formId, TestRemoval removal) =>
        api.PostAsync(
            $"/api/forms/{formId}/audience/people/test/removal",
            new { removal.MembershipIds, removal.AlsoDeleteTestSubmissions, removal.DryRun });

    private static RemovedTestPeople RemovedOf(JsonElement body) =>
        new(
            body.GetProperty("people").GetInt32(),
            body.GetProperty("testSubmissions").GetInt32(),
            body.GetProperty("dryRun").GetBoolean());

    private static async Task<long> TestSubmissionIdAsync(AudienceApi admin, long formId, Participant participant) =>
        IdOf(await OpenTestSubmissionAsync(admin, formId, participant.MembershipId), "submissionId");

    private static async Task<HttpStatusCode[]> SubmissionStatusesAsync(AudienceApi admin, long formId, long[] submissionIds)
    {
        List<HttpStatusCode> statuses = [];
        foreach (var submissionId in submissionIds)
        {
            statuses.Add(await SubmissionStatusAsync(admin, formId, submissionId));
        }

        return [.. statuses];
    }
}
