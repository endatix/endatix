using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Test links (endatix#1422): one request opens a test submission per run for each Test participant and mints a
/// fill-in token for it, all or nothing.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Creating_test_links_opens_a_test_submission_per_run_that_its_token_opens()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var ada = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        var bo = await AddTestParticipantAsync(admin, formId, "bo@example.com");
        var before = DateTime.UtcNow;

        // Act
        var items = await CreateTestLinksAsync(
            admin, formId, new { membershipIds = new[] { bo.MembershipId, ada.MembershipId }, runsEach = 2, expiryMinutes = 120 });

        // Assert
        Assert.Equal(
            [(ada.MembershipId, "ada@example.com", 1), (ada.MembershipId, "ada@example.com", 2),
             (bo.MembershipId, "bo@example.com", 1), (bo.MembershipId, "bo@example.com", 2)],
            items.Select(item => (IdOf(item, "membershipId"), item.GetProperty("identifier").GetString()!, item.GetProperty("run").GetInt32())));
        Assert.Equal(4, items.Select(item => IdOf(item, "submissionId")).Distinct().Count());
        using var anonymous = world.AnonymousClient();
        foreach (var item in items)
        {
            var expiresAt = item.GetProperty("expiresAt").GetDateTime();
            Assert.InRange(expiresAt, before.AddMinutes(119), DateTime.UtcNow.AddMinutes(121));
            var opened = await anonymous.GetFromJsonAsync<JsonElement>(
                ByAccessTokenUrl(formId, item.GetProperty("token").GetString()!), admin.CancellationToken);
            Assert.Equal(IdOf(item, "submissionId"), IdOf(opened, "id"));
            Assert.True(opened.GetProperty("isTestSubmission").GetBoolean());
        }
    }

    [Fact]
    public async Task Creating_test_links_for_every_test_participant_leaves_out_the_real_ones()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "real@example.com");
        await AddTestParticipantAsync(admin, formId, "ada@example.com");
        await AddTestParticipantAsync(admin, formId, "bo@example.com");

        // Act
        var items = await CreateTestLinksAsync(admin, formId, new { membershipIds = (long[]?)null, runsEach = 1, expiryMinutes = 60 });

        // Assert
        Assert.Equal(["ada@example.com", "bo@example.com"], items.Select(item => item.GetProperty("identifier").GetString()));
    }

    [Fact]
    public async Task Creating_test_links_for_every_test_participant_of_a_form_with_none_returns_no_links()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "real@example.com");

        // Act
        var items = await CreateTestLinksAsync(admin, formId, new { membershipIds = (long[]?)null, runsEach = 3, expiryMinutes = 60 });

        // Assert
        Assert.Empty(items);
        Assert.Empty(await LiveSubmissionIdsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Creating_test_links_with_a_real_participant_in_the_set_is_rejected_and_opens_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        var real = await AddParticipantAsync(admin, formId, "real@example.com");

        // Act
        using var response = await admin.PostAsync(
            TestLinksUrl(formId),
            new { membershipIds = new[] { tester.MembershipId, real.MembershipId }, runsEach = 1, expiryMinutes = 60 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await LiveSubmissionIdsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Creating_test_links_for_a_participant_of_another_form_is_not_found_and_opens_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        var elsewhere = await AddTestParticipantAsync(admin, await admin.CreateFormAsync(), "bo@example.com");

        // Act
        using var response = await admin.PostAsync(
            TestLinksUrl(formId),
            new { membershipIds = new[] { tester.MembershipId, elsewhere.MembershipId }, runsEach = 1, expiryMinutes = 60 });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await LiveSubmissionIdsAsync(world.Services, formId));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(11, 60)]
    [InlineData(1, 59)]
    [InlineData(1, 86_401)]
    public async Task Creating_test_links_outside_the_run_or_expiry_bounds_is_rejected(int runsEach, int expiryMinutes)
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var response = await admin.PostAsync(
            TestLinksUrl(formId), new { membershipIds = new[] { tester.MembershipId }, runsEach, expiryMinutes });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_more_than_500_test_submissions_in_one_call_is_rejected_and_opens_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await ImportedFormAsync(admin, new(People: 51, Properties: 1, IsTest: true));

        // Act
        using var response = await admin.PostAsync(
            TestLinksUrl(formId), new { membershipIds = (long[]?)null, runsEach = 10, expiryMinutes = 60 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await LiveSubmissionIdsAsync(world.Services, formId));
    }

    [Fact]
    public async Task An_editor_who_is_not_an_admin_creates_test_links_with_forms_test_and_forms_edit()
    {
        // Arrange
        var world = await TesterWorldAsync([Actions.Access.Hub, Actions.Forms.Test, Actions.Forms.Edit]);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var editor = await TesterAsync(world);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var items = await CreateTestLinksAsync(
            editor, formId, new { membershipIds = new[] { tester.MembershipId }, runsEach = 1, expiryMinutes = 60 });

        // Assert
        using var anonymous = world.AnonymousClient();
        using var opened = await anonymous.GetAsync(
            ByAccessTokenUrl(formId, Assert.Single(items).GetProperty("token").GetString()!), admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
    }

    [Fact]
    public async Task Creating_test_links_with_forms_test_but_not_forms_edit_is_forbidden()
    {
        // Arrange
        var world = await TesterWorldAsync([Actions.Access.Hub, Actions.Forms.View, Actions.Forms.Test]);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var response = await (await TesterAsync(world)).PostAsync(
            TestLinksUrl(formId), new { membershipIds = new[] { tester.MembershipId }, runsEach = 1, expiryMinutes = 60 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creating_test_links_as_a_creator_without_forms_test_is_forbidden()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var creator = await AsAsync(world, TestPersona.Creator);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var response = await creator.PostAsync(
            TestLinksUrl(formId), new { membershipIds = new[] { tester.MembershipId }, runsEach = 1, expiryMinutes = 60 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The token carries <c>submit</c> alone, as Copy test link's does. The API still accepts a later save through
    /// an unstamped token; the respondent page shows the completed state and offers none.
    /// </summary>
    [Fact]
    public async Task A_test_links_token_fills_in_only_and_reopens_its_submission_completed()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        var token = Assert.Single(await CreateTestLinksAsync(
            admin, formId, new { membershipIds = new[] { tester.MembershipId }, runsEach = 1, expiryMinutes = 60 }))
            .GetProperty("token").GetString()!;
        using var anonymous = world.AnonymousClient();

        // Act
        using var completed = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, token), new { isComplete = true, jsonData = """{"q1":"final"}""" }, admin.CancellationToken);
        var reopened = await anonymous.GetFromJsonAsync<JsonElement>(ByAccessTokenUrl(formId, token), admin.CancellationToken);

        // Assert
        Assert.Equal(["submit"], (await ClaimsAsync(world.Services, token)).Permissions);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.True(reopened.GetProperty("isComplete").GetBoolean());
        Assert.Contains("final", reopened.GetProperty("jsonData").GetString());
    }

    [Fact]
    public async Task A_test_links_token_whose_submission_was_deleted_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        var link = Assert.Single(await CreateTestLinksAsync(
            admin, formId, new { membershipIds = new[] { tester.MembershipId }, runsEach = 1, expiryMinutes = 60 }));
        using var deleted = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/submissions/{IdOf(link, "submissionId")}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using var anonymous = world.AnonymousClient();

        // Act
        using var opened = await anonymous.GetAsync(
            ByAccessTokenUrl(formId, link.GetProperty("token").GetString()!), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);
    }

    /// <summary>
    /// The second of three tokens cannot be minted. The first submission was already saved in the call's transaction,
    /// so it is rolled back with the rest: the call opens nothing.
    /// </summary>
    [Fact]
    public async Task A_token_that_cannot_be_minted_part_way_leaves_no_test_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var tester = await AddTestParticipantAsync(admin, formId, "ada@example.com");
        await using var scope = world.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new CreateTestLinksHandler(
            services.GetRequiredService<IAudienceDbContext>(),
            services.GetRequiredService<TestSubjects>(),
            services.GetRequiredService<TestSubmissionOpener>(),
            new FailingSecondMint(services.GetRequiredService<ISubmissionAccessTokenService>()));

        // Act
        var result = await handler.Handle(
            new CreateTestLinksCommand(
                await TenantIdOfAsync(world.Services, formId), formId, [tester.MembershipId], RunsEach: 3, ExpiryMinutes: 60),
            admin.CancellationToken);

        // Assert
        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Empty(await LiveSubmissionIdsAsync(world.Services, formId));
    }

    private const string TESTER_ROLE = "FormTester";
    private const string TESTER_USER_NAME = "tester-user@example.com";

    /// <summary>A world with a user who is not an admin and holds only <paramref name="permissions"/>.</summary>
    private Task<IntegrationTestWorld> TesterWorldAsync(string[] permissions) =>
        fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with
            {
                DefaultPassword = SeedPassword,
                AfterSeed = async (services, seed, cancellationToken) =>
                {
                    var tenantId = seed.TenantIds[0];
                    IntegrationSeedBuilder builder = new(services);
                    await builder.SeedRoleAsync(tenantId, TESTER_ROLE, cancellationToken);
                    await builder.SeedRolePermissionsAsync(tenantId, TESTER_ROLE, permissions, cancellationToken);
                    await builder.SeedUserAsync(
                        tenantId, TESTER_USER_NAME, TESTER_USER_NAME, password: SeedPassword, cancellationToken: cancellationToken);
                    await builder.SeedUserRoleAssignmentAsync(tenantId, TESTER_USER_NAME, TESTER_ROLE, cancellationToken);
                },
            },
            TestContext.Current.CancellationToken);

    private static async Task<AudienceApi> TesterAsync(IntegrationTestWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        return new AudienceApi(
            await world.AsAsync(TestPersona.CustomRole(TESTER_ROLE), userName: TESTER_USER_NAME, cancellationToken: ct), ct);
    }

    private static async Task<SubmissionAccessTokenClaims> ClaimsAsync(IServiceProvider services, string token)
    {
        await using var scope = services.CreateAsyncScope();
        var claims = await scope.ServiceProvider.GetRequiredService<ISubmissionAccessTokenService>()
            .ValidateAccessTokenAsync(token, TestContext.Current.CancellationToken);
        Assert.True(claims.IsSuccess);
        return claims.Value;
    }

    private static string TestLinksUrl(long formId) => $"/api/forms/{formId}/audience/test-links";

    private static async Task<JsonElement[]> CreateTestLinksAsync(AudienceApi api, long formId, object body)
    {
        using var response = await api.PostAsync(TestLinksUrl(formId), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await api.ReadAsync(response)).GetProperty("items").EnumerateArray()];
    }

    /// <summary>Mints as <paramref name="inner"/> does, but fails the second token.</summary>
    private sealed class FailingSecondMint(ISubmissionAccessTokenService inner) : ISubmissionAccessTokenService
    {
        private int _minted;

        public Result<SubmissionAccessTokenDto> GenerateAccessToken(
            long submissionId,
            int expiryMinutes,
            IEnumerable<string> permissions) =>
            ++_minted == 2
                ? Result.Error("The token could not be minted.")
                : inner.GenerateAccessToken(submissionId, expiryMinutes, permissions);

        public Result<SubmissionAccessTokenDto> GenerateAccessToken(SubmissionAccessTokenRequest request) =>
            GenerateAccessToken(request.SubmissionId, request.ExpiryMinutes, request.Permissions);

        public Task<Result<SubmissionAccessTokenClaims>> ValidateAccessTokenAsync(
            string token,
            CancellationToken cancellationToken) =>
            inner.ValidateAccessTokenAsync(token, cancellationToken);
    }
}
