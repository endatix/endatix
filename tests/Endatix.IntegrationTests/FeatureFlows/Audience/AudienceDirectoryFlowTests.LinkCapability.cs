using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.Core.Entities;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A redeemed link hands out a signed access token: it fills in that one submission on a public or private form,
/// without a login, until the link is reissued, the person leaves the form, or the form stops taking submissions.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private static readonly string[] _viewOnly = ["view"];

    [Fact]
    public async Task A_redeemed_link_on_a_private_form_fills_in_and_completes_its_submission_anonymously()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Cleveland");
        await SetFormAsync(admin, formId, new { isPublic = false });
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        var submissionId = IdOf(opened, "submissionId");
        var accessToken = SubmissionAccessTokenOf(opened);

        // Act
        using var access = await AccessAsync(anonymous, formId, accessToken);
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, accessToken), admin.CancellationToken);
        using var saved = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, accessToken),
            new { isComplete = true, jsonData = """{"q1":"done"}""" },
            admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        var grant = await access.Content.ReadFromJsonAsync<JsonElement>(admin.CancellationToken);
        Assert.Equal(submissionId.ToString(), grant.GetProperty("submissionId").GetString());
        Assert.Contains("submission.edit", grant.GetProperty("submissionPermissions").EnumerateArray().Select(item => item.GetString()));
        var expiresAt = opened.GetProperty("submissionAccessTokenExpiresAt").GetDateTime();
        Assert.InRange(expiresAt, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var submission = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);
        Assert.True(submission.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task A_redeemed_links_access_token_opens_nothing_on_another_form()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Parma");
        (var otherFormId, _) = await LinkForPersonWithCityAsync(admin, "Lorain");
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, formId, token));

        // Act
        using var access = await AccessAsync(anonymous, otherFormId, accessToken);
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(otherFormId, accessToken), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, access.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task A_redeemed_links_access_token_is_refused_once_the_person_leaves_the_form()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Mentor");
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, link.FormId, link.Token));
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        using var access = await AccessAsync(anonymous, link.FormId, accessToken);
        using var saved = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(link.FormId, accessToken), new { jsonData = """{"q1":"late"}""" }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, access.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
    }

    [Fact]
    public async Task A_redeemed_links_access_token_is_refused_once_the_link_is_reissued()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Medina");
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, formId, token));
        await ReissueAsync(world.Services, formId);

        // Act
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, accessToken), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, read.StatusCode);
    }

    [Fact]
    public async Task A_removal_refuses_a_used_token_once_the_gate_check_expires_whatever_the_grant_cache_holds()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Wooster");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var accessToken = SubmissionAccessTokenOf(opened);
        using var before = await AccessAsync(anonymous, link.FormId, accessToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var cached = await AccessAsync(anonymous, link.FormId, accessToken);

        // Act
        await ExpireGateCheckAsync(world.Services, IdOf(opened, "submissionId"));
        using var access = await AccessAsync(anonymous, link.FormId, accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, access.StatusCode);
    }

    [Theory]
    [InlineData(false, false, HttpStatusCode.BadRequest)]
    [InlineData(false, true, HttpStatusCode.OK)]
    [InlineData(true, false, HttpStatusCode.BadRequest)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task A_redeemed_links_access_token_follows_the_after_completion_rule(
        bool screenOut,
        bool validAfterCompletion,
        HttpStatusCode expected)
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Elyria");
        await SetTenantSettingsAsync(
            world.Services,
            world.Tenants[0].Id,
            settings => settings.UpdateSubmissionTokenValidAfterCompletion(validAfterCompletion));
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, formId, token));
        object ending = screenOut
            ? new { jsonData = """{"q1":"no"}""", collectionOutcome = "screen_out" }
            : new { jsonData = """{"q1":"done"}""", isComplete = true };
        using var ended = await anonymous.PatchAsJsonAsync(ByAccessTokenUrl(formId, accessToken), ending, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);

        // Act
        using var access = await AccessAsync(anonymous, formId, accessToken);
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, accessToken), admin.CancellationToken);

        // Assert
        Assert.Equal(expected, access.StatusCode);
        Assert.Equal(expected, read.StatusCode);
    }

    [Fact]
    public async Task A_completed_submission_is_not_edited_through_its_links_access_token()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Avon");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        var accessToken = SubmissionAccessTokenOf(opened);
        using var completed = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, accessToken), new { isComplete = true, jsonData = """{"q1":"final"}""" }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        // Act
        using var edited = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, accessToken), new { jsonData = """{"q1":"changed"}""" }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, edited.StatusCode);
        var submission = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{IdOf(opened, "submissionId")}", admin.CancellationToken);
        Assert.Contains("final", submission.GetProperty("jsonData").GetString());
    }

    [Fact]
    public async Task A_staff_access_token_still_reads_a_completed_submission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Berea");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        var submissionId = IdOf(opened, "submissionId");
        using var completed = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(formId, SubmissionAccessTokenOf(opened)), new { isComplete = true }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var issued = await admin.PostAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token", new { expiryMinutes = 60, permissions = _viewOnly });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var staffToken = (await admin.ReadAsync(issued)).GetProperty("token").GetString()!;

        // Act
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, staffToken), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task A_lifetime_that_never_expires_gives_the_links_access_token_the_sixty_day_cap()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Norwalk");
        await SetTenantSettingsAsync(world.Services, world.Tenants[0].Id, settings => settings.UpdateSubmissionTokenExpiry(null));
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);

        // Assert
        var expiresAt = opened.GetProperty("submissionAccessTokenExpiresAt").GetDateTime();
        Assert.InRange(expiresAt, DateTime.UtcNow.AddDays(60).AddMinutes(-5), DateTime.UtcNow.AddDays(60).AddMinutes(5));
    }

    [Fact]
    public async Task A_redeemed_links_access_token_opens_only_its_own_submission_on_the_form()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var firstLink) = await LinkForPersonWithCityAsync(admin, "Ashland");
        using var added = await admin.AddPersonAsync(formId, $"{Guid.NewGuid():N}@example.com");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var secondLink = await IssueLinkAsync(admin, formId);
        using var anonymous = world.AnonymousClient();
        var first = await RedeemAsync(anonymous, formId, firstLink);
        var second = await RedeemAsync(anonymous, formId, secondLink);

        // Act
        var read = await anonymous.GetFromJsonAsync<JsonElement>(
            ByAccessTokenUrl(formId, SubmissionAccessTokenOf(first)), admin.CancellationToken);

        // Assert
        Assert.NotEqual(IdOf(first, "submissionId"), IdOf(second, "submissionId"));
        Assert.Equal(IdOf(first, "submissionId"), IdOf(read, "id"));
    }

    [Fact]
    public async Task A_redeemed_links_access_token_is_refused_at_once_when_the_form_is_deleted()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Bucyrus");
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, formId, token));
        using var before = await AccessAsync(anonymous, formId, accessToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var deleted = await admin.Client.DeleteAsync($"/api/forms/{formId}", admin.CancellationToken);
        Assert.True(deleted.IsSuccessStatusCode);

        // Act
        using var access = await AccessAsync(anonymous, formId, accessToken);
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, accessToken), admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, access.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task A_redeemed_links_access_token_is_refused_at_once_when_the_form_is_disabled()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Sandusky");
        using var anonymous = world.AnonymousClient();
        var accessToken = SubmissionAccessTokenOf(await RedeemAsync(anonymous, formId, token));
        using var before = await AccessAsync(anonymous, formId, accessToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await SetFormAsync(admin, formId, new { isEnabled = false });

        // Act
        using var access = await AccessAsync(anonymous, formId, accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, access.StatusCode);
    }

    [Fact]
    public async Task A_submission_token_on_a_private_form_without_a_session_is_still_unauthorized()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Findlay");
        await SetFormAsync(admin, formId, new { isPublic = false });
        using var anonymous = world.AnonymousClient();
        var submissionToken = AccessTokenOf(await RedeemAsync(anonymous, formId, token));

        // Act
        using var access = await anonymous.GetAsync(
            $"/api/public/forms/{formId}/access?token={submissionToken}&tokenType=SubmissionToken",
            admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
    }

    private static string ByAccessTokenUrl(long formId, string accessToken) =>
        $"/api/forms/{formId}/submissions/by-access-token/{accessToken}";

    private static Task<HttpResponseMessage> AccessAsync(HttpClient anonymous, long formId, string accessToken) =>
        anonymous.GetAsync(
            $"/api/public/forms/{formId}/access?token={accessToken}&tokenType=AccessToken",
            TestContext.Current.CancellationToken);

    private static async Task SetFormAsync(AudienceApi admin, long formId, object changes)
    {
        using var response = await admin.Client.PatchAsJsonAsync($"/api/forms/{formId}", changes, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>What a minute does to the gate's cached check: the link's next use reads the link and person again.</summary>
    private static async Task ExpireGateCheckAsync(IServiceProvider services, long submissionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var link = await scope.ServiceProvider.GetRequiredService<IAudienceDbContext>().Links.AsNoTracking()
            .SingleAsync(item => item.SubmissionId == submissionId, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<HybridCache>()
            .RemoveAsync(LinkAccessTokenGate.CacheKey(submissionId, link.AccessStamp()), cancellationToken);
    }

    /// <summary>
    /// The tenant's settings row, created when the tenant has none. Each test's world is a fresh database, so a change
    /// never reaches another test.
    /// </summary>
    private static async Task SetTenantSettingsAsync(IServiceProvider services, long tenantId, Action<TenantSettings> change)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await app.TenantSettings.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken);
        if (settings is null)
        {
            settings = new TenantSettings(tenantId);
            app.TenantSettings.Add(settings);
        }

        change(settings);
        await app.SaveChangesAsync(cancellationToken);
    }

    /// <summary>What a reissue does to an opened link (endatix#1400): a new token hash, the same submission.</summary>
    private static async Task ReissueAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var updated = await audience.Links
            .Where(link => link.FormId == formId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(link => link.TokenHash, AudienceLink.Hash(Guid.NewGuid().ToString())),
                cancellationToken);
        Assert.Equal(1, updated);
    }
}
