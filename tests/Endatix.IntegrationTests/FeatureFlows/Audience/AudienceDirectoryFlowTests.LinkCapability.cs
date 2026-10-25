using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A redeemed link hands out a signed access token: it fills in that one submission on a public or private form,
/// without a login, until the link is reissued, the person leaves the form, or the form stops taking submissions.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
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
        var accessToken = AccessTokenOf(opened);

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
        Assert.True(opened.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow.AddHours(23));
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
        var accessToken = AccessTokenOf(await RedeemAsync(anonymous, formId, token));

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
        var accessToken = AccessTokenOf(await RedeemAsync(anonymous, link.FormId, link.Token));
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        using var access = await AccessAsync(anonymous, link.FormId, accessToken);
        using var saved = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(link.FormId, accessToken), new { jsonData = """{"q1":"late"}""" }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, access.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, saved.StatusCode);
    }

    [Fact]
    public async Task A_redeemed_links_access_token_is_refused_once_the_link_is_reissued()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Medina");
        using var anonymous = world.AnonymousClient();
        var accessToken = AccessTokenOf(await RedeemAsync(anonymous, formId, token));
        await ReissueAsync(world.Services, formId);

        // Act
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(formId, accessToken), admin.CancellationToken);

        // Assert
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
        var accessToken = AccessTokenOf(await RedeemAsync(anonymous, formId, token));
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
        var submissionId = IdOf(await RedeemAsync(anonymous, formId, token), "submissionId");
        var submissionToken = await StoredTokenAsync(world.Services, submissionId);

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

    private static async Task<string> StoredTokenAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await app.Submissions.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(submission => submission.Id == submissionId)
            .Select(submission => submission.Token!.Value)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
