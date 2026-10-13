using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Personalised links over HTTP: issue once per person, the first anonymous open freezes the
/// person's values on a new submission, a repeat open returns it, and the typed read shows them.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_personalised_link_twice_returns_one_submission_with_the_frozen_values()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();

        // Act
        var first = await RedeemAsync(anonymous, formId, token);
        var second = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.True(first.GetProperty("created").GetBoolean());
        Assert.False(second.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(first, "submissionId"), IdOf(second, "submissionId"));
        Assert.False(first.TryGetProperty("snapshot", out _));
        Assert.False(string.IsNullOrEmpty(first.GetProperty("accessToken").GetString()));
        var submission = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{IdOf(first, "submissionId")}", admin.CancellationToken);
        Assert.Equal(
            "Columbus",
            submission.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
    }

    [Fact]
    public async Task Generating_links_again_issues_none_to_people_who_have_one()
    {
        // Arrange
        var admin = await AdminAsync();
        (var formId, _) = await LinkForPersonWithCityAsync(admin, "Dayton");

        // Act
        using var response = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await admin.ReadAsync(response)).GetArrayLength());
    }

    [Fact]
    public async Task Opening_an_unknown_link_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, new string('a', 64)), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Opening_a_link_claims_the_submission_left_when_the_claim_did_not_commit()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        var orphanId = await LeaveUnclaimedSubmissionAsync(world.Services, formId);
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.False(opened.GetProperty("created").GetBoolean());
        Assert.Equal(orphanId, IdOf(opened, "submissionId"));
        await AssertOnlySubmissionAsync(world.Services, formId, orphanId);
    }

    [Fact]
    public async Task Opening_one_link_many_times_at_once_creates_one_submission_with_one_access_token()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Akron");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await Task.WhenAll(
            Enumerable.Range(0, ParallelOpens).Select(_ => RedeemAsync(anonymous, formId, token)));
        var reopened = await RedeemAsync(anonymous, formId, token);

        // Assert
        var submissionId = IdOf(opened[0], "submissionId");
        Assert.All(opened, item => Assert.Equal(submissionId, IdOf(item, "submissionId")));
        Assert.Equal(1, opened.Count(item => item.GetProperty("created").GetBoolean()));
        Assert.All(opened, item => Assert.Equal(AccessTokenOf(reopened), AccessTokenOf(item)));
        await AssertOnlySubmissionAsync(world.Services, formId, submissionId);
    }

    [Fact]
    public async Task Opening_a_link_whose_submission_was_deleted_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Toledo");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        await DeleteSubmissionAsync(world.Services, IdOf(opened, "submissionId"));

        // Act
        using var again = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    /// <summary>
    /// A form delete that loaded the form's submissions just before the first open inserted its
    /// own leaves that submission live on a deleted form. Reopening it is not found, not a 500.
    /// </summary>
    [Fact]
    public async Task Opening_a_link_whose_form_was_deleted_under_a_live_submission_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Lima");
        using var anonymous = world.AnonymousClient();
        await RedeemAsync(anonymous, formId, token);
        await DeleteOnlyTheFormAsync(world.Services, formId);

        // Act
        using var again = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    /// <summary>A committed submission for the link, with the link still unopened.</summary>
    private static async Task<long> LeaveUnclaimedSubmissionAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var link = await audience.Links.AsNoTracking().SingleAsync(item => item.FormId == formId, cancellationToken);
        var definitionId = await app.Forms
            .Where(form => form.Id == formId)
            .Select(form => form.ActiveDefinitionId)
            .SingleAsync(cancellationToken);
        var submission = Submission.Create(new SubmissionCreateArgs(
            link.TenantId,
            formId,
            definitionId!.Value,
            "{}",
            IsComplete: false,
            StartSubmission: true));
        submission.FreezePersonalization(link.Id, """{"schemaVersion":1,"identifier":"x","variables":{"city":"Columbus"}}""");
        app.Add(submission);
        await app.SaveChangesAsync(cancellationToken);
        return submission.Id;
    }

    private static async Task DeleteSubmissionAsync(IServiceProvider services, long submissionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await app.Submissions.SingleAsync(item => item.Id == submissionId, cancellationToken);
        submission.Delete();
        await app.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The form row only: no domain events, so its submissions, links and people stay.</summary>
    private static async Task DeleteOnlyTheFormAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await app.Forms
            .Where(form => form.Id == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(form => form.IsDeleted, true), cancellationToken);
        Assert.Equal(1, updated);
    }

    private const int ParallelOpens = 8;
    private static string AccessTokenOf(JsonElement opened) => opened.GetProperty("accessToken").GetString()!;

    /// <summary>The form has exactly this submission, live, soft-deleted rows included, and the link names it.</summary>
    private static async Task AssertOnlySubmissionAsync(IServiceProvider services, long formId, long submissionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var rows = await app.Submissions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant])
            .Where(item => item.FormId == formId)
            .Select(item => new { item.Id, item.IsDeleted })
            .ToListAsync(cancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(submissionId, row.Id);
        Assert.False(row.IsDeleted);
        var linked = await audience.Links.AsNoTracking()
            .Where(item => item.FormId == formId)
            .Select(item => item.SubmissionId)
            .SingleAsync(cancellationToken);
        Assert.Equal(submissionId, linked);
    }

    /// <summary>A form with a City property and one person whose city is set; their one link.</summary>
    private static async Task<IssuedLink> LinkForPersonWithCityAsync(
        AudienceApi admin,
        string city,
        bool limitOnePerUser = false)
    {
        var identifier = $"{Guid.NewGuid():N}@example.com";
        (var personUrl, var propertyId) = await admin.PersonWithPropertyAsync(
            new { name = "City", dataType = "text" }, identifier, limitOnePerUser);
        using var saved = await admin.PutValueAsync(personUrl, propertyId, city);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var formId = long.Parse(personUrl.Split('/')[3]);
        return new IssuedLink(formId, personUrl, propertyId, identifier, await IssueLinkAsync(admin, formId));
    }

    /// <summary>The token of the one link issued on a form with one person.</summary>
    private static async Task<string> IssueLinkAsync(AudienceApi admin, long formId)
    {
        using var issued = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var link = Assert.Single((await admin.ReadAsync(issued)).EnumerateArray());
        return link.GetProperty("token").GetString()!;
    }

    private sealed record IssuedLink(long FormId, string PersonUrl, long PropertyId, string Identifier, string Token)
    {
        public void Deconstruct(out long formId, out string token) => (formId, token) = (FormId, Token);
    }

    private static string RedeemUrl(long formId, string token) =>
        $"/api/forms/{formId}/audience/links/{token}/redeem";

    private static async Task<JsonElement> RedeemAsync(HttpClient anonymous, long formId, string token)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
