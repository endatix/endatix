using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Reissuing one person's link (endatix#1400): the link gets a new token and the old one stops opening it. An opened
/// link keeps its submission, so the person resumes their response, and its old access token is refused.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Reissuing_an_unopened_link_gives_a_new_token_and_the_old_one_no_longer_opens()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Akron");
        using var anonymous = world.AnonymousClient();

        // Act
        var reissued = await ReissueLinkAsync(admin, link);

        // Assert
        Assert.Equal(MembershipIdOf(link), IdOf(reissued, "membershipId"));
        var token = TokenOf(reissued);
        Assert.NotEqual(link.Token, token);
        using var old = await anonymous.PostAsJsonAsync(RedeemUrl(link.FormId, link.Token), new { }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        var opened = await RedeemAsync(anonymous, link.FormId, token);
        Assert.True(opened.GetProperty("created").GetBoolean());
    }

    [Fact]
    public async Task Reissuing_an_opened_link_resumes_the_same_submission_and_refuses_the_old_tokens()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Canton");
        using var anonymous = world.AnonymousClient();
        var first = await RedeemAsync(anonymous, link.FormId, link.Token);
        var submissionId = IdOf(first, "submissionId");
        var oldAccessToken = SubmissionAccessTokenOf(first);
        using var before = await AccessAsync(anonymous, link.FormId, oldAccessToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var oldStamp = await LinkStampAsync(world.Services, submissionId);

        // Act
        var token = TokenOf(await ReissueLinkAsync(admin, link));
        await world.Services.GetRequiredService<HybridCache>()
            .RemoveAsync(LinkAccessTokenGate.CacheKey(submissionId, oldStamp), admin.CancellationToken);

        // Assert
        using var oldLink = await anonymous.PostAsJsonAsync(RedeemUrl(link.FormId, link.Token), new { }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, oldLink.StatusCode);
        using var access = await AccessAsync(anonymous, link.FormId, oldAccessToken);
        using var read = await anonymous.GetAsync(ByAccessTokenUrl(link.FormId, oldAccessToken), admin.CancellationToken);
        foreach (var refused in new[] { access, read })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(admin.CancellationToken);
            Assert.Equal("token_revoked", problem.GetProperty("errorCode").GetString());
        }

        var resumed = await RedeemAsync(anonymous, link.FormId, token);
        Assert.False(resumed.GetProperty("created").GetBoolean());
        Assert.Equal(submissionId, IdOf(resumed, "submissionId"));
        await AssertFillsInAsync(anonymous, (link.FormId, submissionId), SubmissionAccessTokenOf(resumed));
    }

    [Fact]
    public async Task Reissuing_a_link_whose_submission_ended_opens_as_ended_and_writes_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Kent");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var submissionId = IdOf(opened, "submissionId");
        using var completed = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(link.FormId, SubmissionAccessTokenOf(opened)),
            new { isComplete = true, jsonData = """{"q1":"done"}""" },
            admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var token = TokenOf(await ReissueLinkAsync(admin, link));
        var storedBefore = await StoredTokenAsync(world.Services, submissionId);

        // Act
        using var again = await anonymous.PostAsJsonAsync(RedeemUrl(link.FormId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<JsonElement>(admin.CancellationToken);
        Assert.Equal("submission_ended", body.GetProperty("errorCode").GetString());
        Assert.Equal(storedBefore, await StoredTokenAsync(world.Services, submissionId));
    }

    [Fact]
    public async Task A_reissue_that_another_reissue_overtook_is_refused_and_the_winners_token_opens()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Mason");
        var command = new ReissueLinkCommand(world.Tenants[0].Id, link.FormId, MembershipIdOf(link));
        var gate = CommandGate.AtLinkUpdate();
        await using var scope = world.Services.CreateAsyncScope();
        await using var gated = GatedAudienceDbContext(scope.ServiceProvider, gate);
        await using var first = new PausedRace<Result<IssuedLinkDto>>(
            gate,
            new ReissueLinkHandler(gated, scope.ServiceProvider.GetRequiredService<IRepository<Form>>())
                .Handle(command, admin.CancellationToken));
        await first.ReachedGateAsync();
        var winner = TokenOf(await ReissueLinkAsync(admin, link));

        // Act
        var overtaken = await first.ReleaseAsync();

        // Assert
        Assert.Equal(ResultStatus.Conflict, overtaken.Status);
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, link.FormId, winner);
        Assert.True(opened.GetProperty("created").GetBoolean());
    }

    [Fact]
    public async Task Reissuing_a_test_participants_link_is_refused()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var tester = await AddPersonWithValuesAsync(admin, formId, new("tester@example.com", IsTest: true, new()));

        // Act
        using var response = await admin.PostAsync(
            $"/api/forms/{formId}/audience/people/{tester.MembershipId}/link/reissue", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(admin.CancellationToken);
        Assert.Contains("test participant", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Reissuing_for_a_person_without_a_link_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var person = await AddPersonWithValuesAsync(admin, formId, new("ada@example.com", IsTest: false, new()));

        // Act
        using var response = await admin.PostAsync(
            $"/api/forms/{formId}/audience/people/{person.MembershipId}/link/reissue", new { });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reissuing_a_removed_persons_or_another_forms_link_is_not_found()
    {
        // Arrange
        var admin = await AdminAsync();
        var removed = await LinkForPersonWithCityAsync(admin, "Lima");
        using var deleted = await admin.Client.DeleteAsync(removed.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var other = await LinkForPersonWithCityAsync(admin, "Toledo");
        var otherFormId = await admin.CreateFormAsync();

        // Act
        using var ofRemoved = await admin.PostAsync($"{removed.PersonUrl}/link/reissue", new { });
        using var ofOtherForm = await admin.PostAsync(
            $"/api/forms/{otherFormId}/audience/people/{MembershipIdOf(other)}/link/reissue", new { });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, ofRemoved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ofOtherForm.StatusCode);
    }

    [Fact]
    public async Task Reissuing_a_link_without_forms_edit_is_forbidden()
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
        var link = await LinkForPersonWithCityAsync(admin, "Xenia");

        // Act
        using var response = await viewer.PostAsync($"{link.PersonUrl}/link/reissue", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<JsonElement> ReissueLinkAsync(AudienceApi admin, IssuedLink link)
    {
        using var response = await admin.PostAsync($"{link.PersonUrl}/link/reissue", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await admin.ReadAsync(response);
    }

    /// <summary>The access stamp the link's tokens carry now, read before a reissue changes it.</summary>
    private static async Task<string> LinkStampAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var link = await scope.ServiceProvider.GetRequiredService<IAudienceDbContext>().Links.AsNoTracking()
            .SingleAsync(item => item.SubmissionId == submissionId, TestContext.Current.CancellationToken);
        return link.AccessStamp();
    }
}
