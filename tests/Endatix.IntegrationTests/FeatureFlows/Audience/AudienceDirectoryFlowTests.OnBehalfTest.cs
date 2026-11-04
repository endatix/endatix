using System.Net;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// A backend creates a test submission on behalf of a tester in one call: an explicit <c>isTest</c> (with
/// <c>forms.test</c>), or the person's test-participant flag on the form. It is personalised as a real one, holds no
/// one-response slot, and stays out of a default export.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task An_on_behalf_test_create_is_personalised_and_leaves_the_persons_real_slot_free()
    {
        // Arrange
        var admin = await AdminAsync();
        var (formId, email) = await FormWithPersonAsync(admin, "Columbus", limitOnePerUser: true);

        // Act
        using var firstTest = await OnBehalfAsync(admin, formId, email, isTest: true);
        using var secondTest = await OnBehalfAsync(admin, formId, email, isTest: true);
        using var real = await OnBehalfAsync(admin, formId, email);

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstTest.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondTest.StatusCode);
        Assert.Equal(HttpStatusCode.Created, real.StatusCode);
        var test = await SubmissionAsync(admin, formId, IdOf(await admin.ReadAsync(firstTest), "id"));
        Assert.True(test.GetProperty("isTestSubmission").GetBoolean());
        Assert.Equal("on_behalf", test.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", CityOf(test));
        Assert.False((await admin.ReadAsync(real)).GetProperty("isTestSubmission").GetBoolean());
    }

    [Fact]
    public async Task An_on_behalf_test_create_without_forms_test_is_forbidden()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var creator = await AsAsync(world, TestPersona.Creator);
        var (formId, email) = await FormWithPersonAsync(admin, "Columbus");

        // Act
        using var response = await OnBehalfAsync(creator, formId, email, isTest: true);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task On_behalf_of_a_test_participant_creates_test_submissions_unless_asked_for_a_real_one()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var creator = await AsAsync(world, TestPersona.Creator);
        var (formId, tester) = await FormWithTestParticipantAsync(admin, "Quito");

        // Act
        using var first = await OnBehalfAsync(creator, formId, tester);
        using var second = await OnBehalfAsync(creator, formId, tester);
        using var real = await OnBehalfAsync(admin, formId, tester, isTest: false);
        using var afterReal = await OnBehalfAsync(admin, formId, tester, isTest: false);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var test = await SubmissionAsync(admin, formId, IdOf(await admin.ReadAsync(first), "id"));
        Assert.True(test.GetProperty("isTestSubmission").GetBoolean());
        Assert.Equal("Quito", CityOf(test));
        Assert.Equal(HttpStatusCode.Created, real.StatusCode);
        Assert.False((await admin.ReadAsync(real)).GetProperty("isTestSubmission").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, afterReal.StatusCode);
    }

    [Fact]
    public async Task An_on_behalf_test_submission_is_left_out_of_a_default_export()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await api.CreateFormAsync();
        var propertyId = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var realEmail = await AddPersonWithCityAsync(api, formId, propertyId, "Columbus", isTest: false);
        var testerEmail = await AddPersonWithCityAsync(api, formId, propertyId, "Quito", isTest: true);
        await CompletedOnBehalfAsync(api, formId, realEmail);
        await CompletedOnBehalfAsync(api, formId, testerEmail);
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));

        // Act
        var byDefault = await CsvExportAsync(api, formId, includeTestSubmissions: null);
        var withTests = await CsvExportAsync(api, formId, includeTestSubmissions: true);

        // Assert
        Assert.Contains(realEmail, byDefault);
        Assert.DoesNotContain(testerEmail, byDefault);
        Assert.Contains(testerEmail, withTests);
    }

    /// <summary>A one-per-user form with a City property and one test participant whose city is set.</summary>
    private static async Task<(long FormId, string Email)> FormWithTestParticipantAsync(AudienceApi admin, string city)
    {
        var formId = await admin.CreateFormAsync(limitOnePerUser: true);
        var propertyId = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        return (formId, await AddPersonWithCityAsync(admin, formId, propertyId, city, isTest: true));
    }

    private static async Task<string> AddPersonWithCityAsync(
        AudienceApi admin,
        long formId,
        long propertyId,
        string city,
        bool isTest)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var added = await admin.PostAsync(
            $"/api/forms/{formId}/audience/people",
            new { identifier = email, values = new Dictionary<string, string> { [$"{propertyId}"] = city }, isTest });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> OnBehalfAsync(AudienceApi caller, long formId, string businessId, bool isTest) =>
        caller.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new
            {
                isTest,
                submitter = new { externalSubjectId = businessId, displayId = businessId, authProvider = "Integration" },
            });
}
