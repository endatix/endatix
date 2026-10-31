using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_disabled_form_does_not_open_until_it_is_enabled()
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
    public async Task Opening_a_link_after_the_form_is_disabled_is_not_found_and_keeps_the_submission()
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
    public async Task Opening_a_link_for_a_person_removed_before_open_is_not_found()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Akron");
        (var formId, var token) = link;
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
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
    public async Task Opening_a_link_after_the_person_was_removed_is_not_found_and_writes_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Toledo");
        (var formId, var token) = link;
        using var anonymous = world.AnonymousClient();
        var submissionId = IdOf(await RedeemAsync(anonymous, formId, token), "submissionId");
        using var removed = await admin.Client.DeleteAsync(link.PersonUrl, admin.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var before = await StoredTokenAsync(world.Services, submissionId);

        // Act
        using var again = await anonymous.PostAsJsonAsync(RedeemUrl(formId, token), new { }, admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(before, await StoredTokenAsync(world.Services, submissionId));
        await AssertOnlySubmissionAsync(world.Services, formId, submissionId);
    }

    /// <summary>The submission's stored token and its expiry: an open that extends it changes the expiry.</summary>
    private static async Task<(string? Value, DateTime? ExpiresAt)> StoredTokenAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await app.Submissions.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(item => item.Id == submissionId, TestContext.Current.CancellationToken);
        return (submission.Token?.Value, submission.Token?.ExpiresAt);
    }

    [Fact]
    public async Task Opening_a_link_pins_the_definition_current_at_the_first_open()
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
    public async Task Opening_a_link_freezes_audience_values_at_the_first_open()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var formId, var personUrl, var propertyId, _, var token) = await LinkForPersonWithCityAsync(admin, "Columbus");
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
}
