using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// An editor deletes a participant's ended response on a one-response form: the person's link, reissued or not,
/// opens a new, empty response, and that one is their only live response.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_link_whose_ended_response_was_deleted_opens_a_new_response()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Parma", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var deletedId = await EndAndDeleteResponseAsync(admin, anonymous, link);

        // Act
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);

        // Assert
        await AssertNewLiveResponseAsync(world.Services, link.FormId, opened, deletedId);
        await AssertFillsInAsync(anonymous, (link.FormId, IdOf(opened, "submissionId")), SubmissionAccessTokenOf(opened));
    }

    [Fact]
    public async Task Opening_a_reissued_link_whose_ended_response_was_deleted_opens_a_new_response()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Lorain", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var deletedId = await EndAndDeleteResponseAsync(admin, anonymous, link);
        var token = TokenOf(await ReissueLinkAsync(admin, link));

        // Act
        var opened = await RedeemAsync(anonymous, link.FormId, token);
        var reopened = await RedeemAsync(anonymous, link.FormId, token);

        // Assert
        await AssertNewLiveResponseAsync(world.Services, link.FormId, opened, deletedId);
        Assert.False(reopened.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(opened, "submissionId"), IdOf(reopened, "submissionId"));
    }

    /// <summary>Opens the link, completes the response, and deletes it as an editor does from the grid.</summary>
    private static async Task<long> EndAndDeleteResponseAsync(AudienceApi admin, HttpClient anonymous, IssuedLink link)
    {
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var submissionId = IdOf(opened, "submissionId");
        using var completed = await anonymous.PatchAsJsonAsync(
            ByAccessTokenUrl(link.FormId, SubmissionAccessTokenOf(opened)),
            new { isComplete = true, jsonData = """{"q1":"done"}""" },
            admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var deleted = await admin.Client.DeleteAsync(
            $"/api/forms/{link.FormId}/submissions/{submissionId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        return submissionId;
    }

    /// <summary>The open created a response other than the deleted one, the link names it, and it is the form's only live one.</summary>
    private static async Task AssertNewLiveResponseAsync(
        IServiceProvider services,
        long formId,
        JsonElement opened,
        long deletedId)
    {
        var submissionId = IdOf(opened, "submissionId");
        Assert.True(opened.GetProperty("created").GetBoolean());
        Assert.NotEqual(deletedId, submissionId);

        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var live = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Submissions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(item => item.FormId == formId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        Assert.Equal([submissionId], live);
        var linked = await scope.ServiceProvider.GetRequiredService<IAudienceDbContext>().Links.AsNoTracking()
            .Where(item => item.FormId == formId)
            .Select(item => item.SubmissionId)
            .SingleAsync(cancellationToken);
        Assert.Equal(submissionId, linked);
    }
}
