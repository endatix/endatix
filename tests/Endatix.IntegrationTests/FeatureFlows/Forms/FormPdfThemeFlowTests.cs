using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// A form's "use the form's theme on its PDFs" setting: set through the form PATCH, and submission details
/// (by id and by share-link access token) return the theme only while it is on. The forms are private, so the
/// share-link path is the only way an anonymous PDF renderer can get the theme.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class FormPdfThemeFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string THEME_JSON = """{"cssVariables":{"--sjs-primary-backcolor":"#123456"}}""";
    private const string FORM_JSON = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";
    private static readonly string[] _viewOnly = ["view"];

    [Fact]
    public async Task Setting_on_returns_the_theme_by_id_and_by_access_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var (formId, storedTheme) = await CreatePrivateFormWithThemeAsync(admin);

        var form = await PatchFormAsync(admin, formId, new { pdfUsesFormTheme = true });
        Assert.True(form.GetProperty("pdfUsesFormTheme").GetBoolean());
        var submissionId = await CreateSubmissionAsync(admin, formId);

        var byId = await GetDefinitionAsync(admin, $"/api/forms/{formId}/submissions/{submissionId}");
        var token = await CreateAccessTokenAsync(admin, formId, submissionId);
        using var anonymous = world.AnonymousClient();
        var byToken = await GetDefinitionAsync(
            anonymous, $"/api/forms/{formId}/submissions/by-access-token/{token}");

        foreach (var definition in new[] { byId, byToken })
        {
            Assert.True(definition.GetProperty("pdfUsesFormTheme").GetBoolean());
            Assert.True(JsonNode.DeepEquals(
                JsonNode.Parse(storedTheme), JsonNode.Parse(definition.GetProperty("themeModel").GetString()!)));
        }

        var active = await GetJsonAsync(admin, $"/api/forms/{formId}/definition");
        Assert.True(active.GetProperty("pdfUsesFormTheme").GetBoolean());
    }

    [Fact]
    public async Task Setting_off_returns_no_theme()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var (formId, _) = await CreatePrivateFormWithThemeAsync(admin);
        var submissionId = await CreateSubmissionAsync(admin, formId);

        var byId = await GetDefinitionAsync(admin, $"/api/forms/{formId}/submissions/{submissionId}");
        var token = await CreateAccessTokenAsync(admin, formId, submissionId);
        using var anonymous = world.AnonymousClient();
        var byToken = await GetDefinitionAsync(
            anonymous, $"/api/forms/{formId}/submissions/by-access-token/{token}");

        foreach (var definition in new[] { byId, byToken })
        {
            Assert.False(definition.GetProperty("pdfUsesFormTheme").GetBoolean());
            Assert.Equal(JsonValueKind.Null, definition.GetProperty("themeModel").ValueKind);
        }
    }

    /// <returns>The form id, and the theme JSON as the API stored it (it normalizes what it is sent, and PostgreSQL reorders its keys).</returns>
    private static async Task<(string FormId, string StoredTheme)> CreatePrivateFormWithThemeAsync(HttpClient admin)
    {
        var ct = TestContext.Current.CancellationToken;
        using var themeResponse = await admin.PostAsJsonAsync(
            "/api/themes", new { name = $"pdf-theme-{Guid.NewGuid():N}", jsonData = THEME_JSON }, ct);
        themeResponse.EnsureSuccessStatusCode();
        var theme = await ReadJsonAsync(themeResponse);
        var themeId = theme.GetProperty("id").GetString()!;
        var storedTheme = theme.GetProperty("jsonData").GetString()!;
        Assert.Contains("#123456", storedTheme);

        using var formResponse = await admin.PostAsJsonAsync(
            "/api/forms",
            new { name = $"pdf-theme-form-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FORM_JSON },
            ct);
        formResponse.EnsureSuccessStatusCode();
        var formId = (await ReadJsonAsync(formResponse)).GetProperty("id").GetString()!;

        var form = await PatchFormAsync(admin, formId, new { themeId = long.Parse(themeId) });
        Assert.False(form.GetProperty("isPublic").GetBoolean());
        Assert.False(form.GetProperty("pdfUsesFormTheme").GetBoolean());
        return (formId, storedTheme);
    }

    private static async Task<JsonElement> PatchFormAsync(HttpClient admin, string formId, object changes)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await admin.PatchAsJsonAsync($"/api/forms/{formId}", changes, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<string> CreateSubmissionAsync(HttpClient admin, string formId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = """{"q1":"answer"}""" },
            ct);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateAccessTokenAsync(
        HttpClient admin, string formId, string submissionId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions = _viewOnly },
            ct);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> GetDefinitionAsync(HttpClient client, string url) =>
        (await GetJsonAsync(client, url)).GetProperty("formDefinition");

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }
}
