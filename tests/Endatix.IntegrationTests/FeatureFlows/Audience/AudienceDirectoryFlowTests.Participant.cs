using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// One participant on a form, read by member id: their values, Link status, and the submissions that are theirs on
/// that form (the link's, their Submitter's, and test submissions opened as them), newest first.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Reading_a_participant_returns_their_link_on_behalf_and_test_submissions_newest_first()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithCityAsync(admin);
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var other = await AddParticipantAsync(admin, formId, "other@example.com");
        var token = Assert.Single(await IssueLinksByIdentifierAsync(admin, formId), link => link.Key == ada.Identifier).Value;
        using var anonymous = world.AnonymousClient();
        var linkId = IdOf(await RedeemAsync(anonymous, formId, token), "submissionId");
        var onBehalfId = await OnBehalfOfAsync(admin, formId, ada.Identifier);
        var testId = IdOf(await OpenTestSubmissionAsync(admin, formId, ada.MembershipId), "submissionId");
        await OnBehalfOfAsync(admin, formId, other.Identifier);
        await OpenTestSubmissionAsync(admin, formId, other.MembershipId);

        // Act
        var participant = await GetParticipantAsync(admin, formId, ada.MemberId);

        // Assert
        Assert.Equal(ada.Identifier, participant.GetProperty("identifier").GetString());
        Assert.Equal(ada.MembershipId, IdOf(participant, "membershipId"));
        Assert.Equal("opened", StatusOf(participant.GetProperty("link")));
        Assert.Equal(linkId, IdOf(participant.GetProperty("link"), "submissionId"));
        Assert.Equal(3, participant.GetProperty("submissionTotal").GetInt32());
        Assert.Equal(
            [(testId, "test", true), (onBehalfId, "on_behalf", false), (linkId, "link", false)],
            SubmissionRows(participant));
    }

    [Fact]
    public async Task Reading_a_test_participant_returns_only_their_live_test_submissions()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithCityAsync(admin);
        var tester = await AddParticipantAsync(admin, formId, "tester@example.com", isTest: true);
        var otherTester = await AddParticipantAsync(admin, formId, "other-tester@example.com", isTest: true);
        var first = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var deleted = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        var last = IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId");
        await OpenTestSubmissionAsync(admin, formId, otherTester.MembershipId);
        await DeleteSubmissionAsync(world.Services, deleted);

        // Act
        var participant = await GetParticipantAsync(admin, formId, tester.MemberId);

        // Assert
        Assert.True(participant.GetProperty("isTest").GetBoolean());
        Assert.Equal("none", StatusOf(participant.GetProperty("link")));
        Assert.Equal(2, participant.GetProperty("submissionTotal").GetInt32());
        Assert.Equal([(last, "test", true), (first, "test", true)], SubmissionRows(participant));
    }

    [Fact]
    public async Task A_participants_submission_without_a_snapshot_has_no_source()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var personalised = await OnBehalfOfAsync(admin, formId, ada.Identifier);
        using var removed = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/properties/{propertyId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var plain = await OnBehalfOfAsync(admin, formId, ada.Identifier);

        // Act
        var participant = await GetParticipantAsync(admin, formId, ada.MemberId);

        // Assert
        Assert.Equal([(plain, null, false), (personalised, "on_behalf", false)], SubmissionRows(participant));
    }

    [Fact]
    public async Task Reading_a_participant_returns_their_newest_fifty_submissions_and_the_total()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        var tester = await AddParticipantAsync(admin, formId, "tester@example.com", isTest: true);
        var opened = new List<long>();
        for (var i = 0; i < 51; i++)
        {
            opened.Add(IdOf(await OpenTestSubmissionAsync(admin, formId, tester.MembershipId), "submissionId"));
        }

        // Act
        var participant = await GetParticipantAsync(admin, formId, tester.MemberId);

        // Assert
        Assert.Equal(51, participant.GetProperty("submissionTotal").GetInt32());
        Assert.Equal(
            opened.AsEnumerable().Reverse().Take(50).ToArray(),
            SubmissionRows(participant).Select(row => row.Id).ToArray());
    }

    [Fact]
    public async Task Reading_a_participant_through_another_form_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        var otherFormId = await admin.CreateFormAsync();

        // Act
        using var response = await GetParticipantResponseAsync(admin, otherFormId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_removed_participant_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");
        using var removed = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{ada.MembershipId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        using var response = await GetParticipantResponseAsync(admin, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_another_tenants_participant_is_not_found()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var stranger = new AudienceApi(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        var formId = await owner.CreateFormAsync();
        var ada = await AddParticipantAsync(owner, formId, "ada@example.com");

        // Act
        using var response = await GetParticipantResponseAsync(stranger, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_participant_without_forms_edit_is_forbidden()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword, AfterSeed = SeedViewerAsync },
            TestContext.Current.CancellationToken);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var viewer = new AudienceApi(
            await world.AsAsync(
                TestPersona.CustomRole(ViewerRole), userName: ViewerUserName, cancellationToken: admin.CancellationToken),
            admin.CancellationToken);
        var formId = await admin.CreateFormAsync();
        var ada = await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var response = await GetParticipantResponseAsync(viewer, formId, ada.MemberId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private const string ViewerRole = "FormViewer";
    private const string ViewerUserName = "viewer@example.com";

    /// <summary>A user who can view forms but not edit them.</summary>
    private static async Task SeedViewerAsync(
        IServiceProvider services,
        StandardSeedResult seed,
        CancellationToken cancellationToken)
    {
        var tenantId = seed.TenantIds[0];
        IntegrationSeedBuilder builder = new(services);
        await builder.SeedRoleAsync(tenantId, ViewerRole, cancellationToken);
        await builder.SeedRolePermissionsAsync(
            tenantId, ViewerRole, [Actions.Access.Hub, Actions.Forms.View], cancellationToken);
        await builder.SeedUserAsync(
            tenantId, ViewerUserName, ViewerUserName, password: SeedPassword, cancellationToken: cancellationToken);
        await builder.SeedUserRoleAssignmentAsync(tenantId, ViewerUserName, ViewerRole, cancellationToken);
    }

    /// <summary>A form with a property: on-behalf creates personalise, and bind the Submitter, only on such a form.</summary>
    private static async Task<long> FormWithCityAsync(AudienceApi admin)
    {
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        return formId;
    }

    private sealed record Participant(long MembershipId, long MemberId, string Identifier);

    private static async Task<Participant> AddParticipantAsync(
        AudienceApi admin,
        long formId,
        string identifier,
        bool isTest = false)
    {
        using var added = await admin.PostAsync($"/api/forms/{formId}/audience/people", new { identifier, isTest });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var person = await admin.ReadAsync(added);
        return new Participant(IdOf(person, "membershipId"), IdOf(person, "audienceMemberId"), identifier);
    }

    private static async Task<long> OnBehalfOfAsync(AudienceApi admin, long formId, string identifier)
    {
        using var created = await OnBehalfAsync(admin, formId, identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return IdOf(await admin.ReadAsync(created), "id");
    }

    private static Task<HttpResponseMessage> GetParticipantResponseAsync(AudienceApi api, long formId, long memberId) =>
        api.Client.GetAsync($"/api/forms/{formId}/audience/people/{memberId}", api.CancellationToken);

    private static async Task<JsonElement> GetParticipantAsync(AudienceApi api, long formId, long memberId)
    {
        using var response = await GetParticipantResponseAsync(api, formId, memberId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await api.ReadAsync(response);
    }

    private static (long Id, string? Source, bool IsTest)[] SubmissionRows(JsonElement participant) =>
        participant.GetProperty("submissions").EnumerateArray()
            .Select(submission => (
                IdOf(submission, "id"),
                submission.TryGetProperty("snapshotSource", out var source) ? source.GetString() : null,
                submission.GetProperty("isTest").GetBoolean()))
            .ToArray();
}
