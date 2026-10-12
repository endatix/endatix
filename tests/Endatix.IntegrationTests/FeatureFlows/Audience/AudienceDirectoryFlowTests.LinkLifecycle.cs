using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task RedeemLink_FormDisabled_DoesNotOpenUntilEnabled()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var disabled = await admin.Client.PatchAsJsonAsync($"/api/forms/{formId}", new { isEnabled = false }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        using var anonymous = world.AnonymousClient();

        // Act
        using var refused = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);
        var stillUnopened = await LinkSubmissionIdAsync(world.Services, formId);
        using var enabled = await admin.Client.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { isEnabled = true }, admin.CancellationToken);
        var opened = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.Null(stillUnopened);
        Assert.True(opened.GetProperty("created").GetBoolean());
    }

    [Fact]
    public async Task RedeemLink_FormDisabledAfterOpen_ReturnsNotFoundAndKeepsTheSubmission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Dayton");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        var submissionId = IdOf(opened, "submissionId");
        using var disabled = await admin.Client.PatchAsJsonAsync($"/api/forms/{formId}", new { isEnabled = false }, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        // Act
        using var again = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);
        using var kept = await admin.Client.GetAsync(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    [Fact]
    public async Task RedeemLink_PersonRemovedBeforeOpen_ReturnsNotFound()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var membershipId, var token) = await IssuedLinkAsync(admin, "Akron");
        using var removed = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var anonymous = world.AnonymousClient();

        // Act
        using var response = await anonymous.PostAsJsonAsync(
            RedeemUrl(formId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await LinkSubmissionIdAsync(world.Services, formId));
    }

    [Fact]
    public async Task RedeemLink_PersonRemovedAfterOpen_ResumesTheSubmission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var membershipId, var token) = await IssuedLinkAsync(admin, "Toledo");
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        using var removed = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // Act
        var again = await RedeemAsync(anonymous, formId, token);

        // Assert
        Assert.False(again.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(opened, "submissionId"), IdOf(again, "submissionId"));
    }

    [Fact]
    public async Task RedeemLink_OneResponseForm_OpensTheExistingOnBehalfSubmission()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync(limitOnePerUser: true);
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        const string person = "cg-000001-01@example.com";
        using var added = await admin.AddPersonAsync(formId, person);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var personUrl = $"/api/forms/{formId}/audience/people/{IdOf(await admin.ReadAsync(added), "membershipId")}";
        using var saved = await admin.PutValueAsync(personUrl, propertyId, "Columbus");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var created = await admin.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new { submitter = new { externalSubjectId = person, displayId = person, authProvider = "Integration" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = IdOf(await admin.ReadAsync(created), "id");
        using var issued = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        var token = Assert.Single((await admin.ReadAsync(issued)).EnumerateArray()).GetProperty("token").GetString()!;
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);
        var read = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{submissionId}", admin.CancellationToken);

        // Assert
        Assert.False(opened.GetProperty("created").GetBoolean());
        Assert.Equal(submissionId, IdOf(opened, "submissionId"));
        Assert.Equal("on_behalf", read.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", read.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
    }

    [Fact]
    public async Task RedeemLink_PublishAfterIssue_PinsTheDefinitionFromTheFirstOpen()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        var published = await PublishAsync(admin, formId, """{"title":"second"}""");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);
        await PublishAsync(admin, formId, """{"title":"third"}""");
        var again = await RedeemAsync(anonymous, formId, token);
        var read = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{IdOf(opened, "submissionId")}", admin.CancellationToken);

        // Assert
        Assert.Equal(published.ToString(), read.GetProperty("formDefinitionId").GetString());
        Assert.Equal(IdOf(opened, "submissionId"), IdOf(again, "submissionId"));
    }

    [Fact]
    public async Task RedeemLink_AudienceEdits_FreezeAtFirstOpen()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var membershipId, var propertyId, var token) = await IssuedLinkWithPropertyAsync(admin, "Columbus");
        var personUrl = $"/api/forms/{formId}/audience/people/{membershipId}";
        using var edited = await admin.PutValueAsync(personUrl, propertyId, "Dayton");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var ageId = await admin.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        using var aged = await admin.PutValueAsync(personUrl, ageId, "65");
        Assert.Equal(HttpStatusCode.OK, aged.StatusCode);
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, token);
        using var later = await admin.PutValueAsync(personUrl, propertyId, "Akron");
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        await admin.CreatePropertyAsync(formId, new { name = "Region", dataType = "text" });

        // Act
        var read = await admin.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/submissions/{IdOf(opened, "submissionId")}", admin.CancellationToken);
        var variables = read.GetProperty("personalization").GetProperty("variables");

        // Assert
        Assert.Equal("Dayton", variables.GetProperty("city").GetString());
        Assert.Equal(65, variables.GetProperty("age").GetInt32());
        Assert.False(variables.TryGetProperty("region", out _));
    }

    private static string RedeemUrl(long formId, string token) =>
        $"/api/forms/{formId}/audience/links/{token}/redeem";

    private static async Task<long> PublishAsync(AudienceApi admin, long formId, string jsonData)
    {
        using var response = await admin.PostAsync(
            $"/api/forms/{formId}/definitions",
            new { isDraft = false, jsonData });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return IdOf(await admin.ReadAsync(response), "id");
    }

    private static async Task<long?> LinkSubmissionIdAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await audience.Links.AsNoTracking()
            .Where(item => item.FormId == formId)
            .Select(item => item.SubmissionId)
            .SingleAsync(cancellationToken);
    }

    private static async Task<(long FormId, long MembershipId, string Token)> IssuedLinkAsync(
        AudienceApi admin,
        string city)
    {
        (var formId, var membershipId, _, var token) = await IssuedLinkWithPropertyAsync(admin, city);
        return (formId, membershipId, token);
    }

    private static async Task<(long FormId, long MembershipId, long PropertyId, string Token)> IssuedLinkWithPropertyAsync(
        AudienceApi admin,
        string city)
    {
        var formId = await admin.CreateFormAsync();
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        using var added = await admin.AddPersonAsync(formId, $"{Guid.NewGuid():N}@example.com");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var membershipId = IdOf(await admin.ReadAsync(added), "membershipId");
        using var saved = await admin.PutValueAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", propertyId, city);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var issued = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var token = Assert.Single((await admin.ReadAsync(issued)).EnumerateArray()).GetProperty("token").GetString()!;
        return (formId, membershipId, propertyId, token);
    }
}
