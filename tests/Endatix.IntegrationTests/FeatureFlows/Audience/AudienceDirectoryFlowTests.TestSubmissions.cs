using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Test as this person and Start over: each call opens a new test submission on the current definition with the
/// person's current values, and leaves the person's personalised link and Submitter as they were.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_test_submission_twice_opens_two_and_the_second_has_the_changed_value()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var membershipId = MembershipIdOf(person);

        // Act
        var first = await OpenTestSubmissionAsync(admin, person.FormId, membershipId);
        using var changed = await admin.PutValueAsync(person.PersonUrl, person.PropertyId, "Dayton");
        var second = await OpenTestSubmissionAsync(admin, person.FormId, membershipId);

        // Assert
        Assert.NotEqual(IdOf(first, "submissionId"), IdOf(second, "submissionId"));
        Assert.False(string.IsNullOrEmpty(first.GetProperty("token").GetString()));
        var firstRead = await SubmissionAsync(admin, person.FormId, IdOf(first, "submissionId"));
        var secondRead = await SubmissionAsync(admin, person.FormId, IdOf(second, "submissionId"));
        Assert.True(firstRead.GetProperty("isTestSubmission").GetBoolean());
        Assert.Equal("test", firstRead.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", CityOf(firstRead));
        Assert.Equal("Dayton", CityOf(secondRead));
    }

    [Fact]
    public async Task Opening_test_submissions_leaves_the_persons_link_and_submitters_unchanged()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        await RedeemAsync(anonymous, person.FormId, person.Token);
        var before = await LinkStateAsync(world.Services, person.FormId);

        // Act
        await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));

        // Assert
        Assert.Equal(before, await LinkStateAsync(world.Services, person.FormId));
    }

    [Fact]
    public async Task Opening_a_test_submission_without_forms_test_is_forbidden()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var creator = await AsAsync(world, TestPersona.Creator);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");

        // Act
        using var response = await creator.PostAsync(
            TestSubmissionsUrl(person.FormId), new { membershipId = MembershipIdOf(person) });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test_submissions_are_left_out_of_a_default_export()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, realEmail) = await FormWithPersonAsync(api, "Columbus");
        var testEmail = $"{Guid.NewGuid():N}@example.com";
        using var added = await api.AddPersonAsync(formId, testEmail);
        await CompletedOnBehalfAsync(api, formId, realEmail);
        await OpenTestSubmissionAsync(api, formId, IdOf(await api.ReadAsync(added), "membershipId"));
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        await ExpectOkAsync(api.PostAsync(
            $"/api/forms/{formId}/submissions/backfill", new { completionScope = "incomplete" }));

        // Act
        var byDefault = await CsvExportAsync(api, formId, includeTestSubmissions: null);
        var withTests = await CsvExportAsync(api, formId, includeTestSubmissions: true);

        // Assert
        Assert.Contains(realEmail, byDefault);
        Assert.DoesNotContain(testEmail, byDefault);
        Assert.Contains(testEmail, withTests);
    }

    [Fact]
    public async Task Starting_over_deletes_the_test_submission_and_opens_a_new_one_with_current_values()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));
        var openedId = IdOf(opened, "submissionId");
        using var changed = await admin.PutValueAsync(person.PersonUrl, person.PropertyId, "Dayton");

        // Act
        using var restarted = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { });

        // Assert
        Assert.Equal(HttpStatusCode.Created, restarted.StatusCode);
        var next = await admin.ReadAsync(restarted);
        Assert.NotEqual(openedId, IdOf(next, "submissionId"));
        Assert.False(string.IsNullOrEmpty(next.GetProperty("token").GetString()));
        Assert.Equal("Dayton", CityOf(await SubmissionAsync(admin, person.FormId, IdOf(next, "submissionId"))));
        using var old = await admin.Client.GetAsync(
            $"/api/forms/{person.FormId}/submissions/{openedId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    [Fact]
    public async Task Starting_over_a_submission_not_opened_as_a_test_is_rejected()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();
        var linkSubmissionId = IdOf(await RedeemAsync(anonymous, person.FormId, person.Token), "submissionId");

        // Act
        using var response = await admin.PostAsync(RestartUrl(person.FormId, linkSubmissionId), new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Starting_over_after_the_person_is_removed_is_not_found_and_keeps_the_submission()
    {
        // Arrange
        var admin = await AdminAsync();
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");
        var openedId = IdOf(await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person)), "submissionId");
        using var removed = await admin.Client.DeleteAsync(person.PersonUrl, admin.CancellationToken);

        // Act
        using var response = await admin.PostAsync(RestartUrl(person.FormId, openedId), new { });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var kept = await admin.Client.GetAsync(
            $"/api/forms/{person.FormId}/submissions/{openedId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    private static string TestSubmissionsUrl(long formId) => $"/api/forms/{formId}/audience/test-submissions";

    private static string RestartUrl(long formId, long submissionId) =>
        $"{TestSubmissionsUrl(formId)}/{submissionId}/restart";

    private static long MembershipIdOf(IssuedLink person) => long.Parse(person.PersonUrl.Split('/')[^1]);

    private static string? CityOf(JsonElement submission) =>
        submission.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString();

    private static async Task<JsonElement> OpenTestSubmissionAsync(AudienceApi admin, long formId, long membershipId)
    {
        using var response = await admin.PostAsync(TestSubmissionsUrl(formId), new { membershipId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadAsync(response);
    }

    private static Task<JsonElement> SubmissionAsync(AudienceApi admin, long formId, long submissionId) =>
        admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);

    private static async Task<string> CsvExportAsync(AudienceApi api, long formId, bool? includeTestSubmissions)
    {
        var formats = await api.Client.GetFromJsonAsync<JsonElement>("/api/settings/export-formats", api.CancellationToken);
        var exportFormatId = IdOf(formats.EnumerateArray().First(format => format.GetProperty("wireKey").GetString() == "csv"), "id");
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportFormatId, includeTestSubmissions });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(api.CancellationToken);
    }

    /// <summary>The form's links as stored, and the tenant's Submitters with the members bound to them.</summary>
    private static async Task<string> LinkStateAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var links = await audience.Links.AsNoTracking()
            .Where(link => link.FormId == formId)
            .OrderBy(link => link.Id)
            .Select(link => new { link.Id, link.SubmissionId, link.OpenedAt, link.TokenHash })
            .ToListAsync(cancellationToken);
        var tenantId = await app.Forms.Where(form => form.Id == formId).Select(form => form.TenantId)
            .SingleAsync(cancellationToken);
        var submitters = await app.Submitters.AsNoTracking()
            .Where(submitter => submitter.TenantId == tenantId)
            .OrderBy(submitter => submitter.Id)
            .Select(submitter => submitter.Id)
            .ToListAsync(cancellationToken);
        var bound = await audience.Members.AsNoTracking()
            .Where(member => member.TenantId == tenantId)
            .OrderBy(member => member.Id)
            .Select(member => new { member.Id, member.SubmitterId })
            .ToListAsync(cancellationToken);
        Assert.NotEmpty(links);
        Assert.NotEmpty(submitters);
        return JsonSerializer.Serialize(new { links, submitters, bound });
    }
}
