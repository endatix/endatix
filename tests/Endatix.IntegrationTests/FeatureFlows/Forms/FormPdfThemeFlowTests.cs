using System.Net.Http.Json;
using System.Text.Json;
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
    private const string ThemeJson = """{"cssVariables":{"--sjs-primary-backcolor":"#123456"}}""";
    private const string FormJson = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";

    [Fact]
    public async Task Setting_on_returns_the_theme_by_id_and_by_access_token()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using HttpClient admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        string formId = await CreatePrivateFormWithThemeAsync(admin);

        JsonElement form = await PatchFormAsync(admin, formId, new { pdfUsesFormTheme = true });
        Assert.True(form.GetProperty("pdfUsesFormTheme").GetBoolean());
        string submissionId = await CreateSubmissionAsync(admin, formId);

        JsonElement byId = await GetDefinitionAsync(admin, $"/api/forms/{formId}/submissions/{submissionId}");
        string token = await CreateAccessTokenAsync(admin, formId, submissionId);
        using HttpClient anonymous = world.AnonymousClient();
        JsonElement byToken = await GetDefinitionAsync(
            anonymous, $"/api/forms/{formId}/submissions/by-access-token/{token}");

        foreach (JsonElement definition in new[] { byId, byToken })
        {
            Assert.True(definition.GetProperty("pdfUsesFormTheme").GetBoolean());
            Assert.Equal(ThemeJson, definition.GetProperty("themeModel").GetString());
        }

        JsonElement active = await GetJsonAsync(admin, $"/api/forms/{formId}/definition");
        Assert.True(active.GetProperty("pdfUsesFormTheme").GetBoolean());
    }

    [Fact]
    public async Task Setting_off_returns_no_theme()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using HttpClient admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        string formId = await CreatePrivateFormWithThemeAsync(admin);
        string submissionId = await CreateSubmissionAsync(admin, formId);

        JsonElement byId = await GetDefinitionAsync(admin, $"/api/forms/{formId}/submissions/{submissionId}");
        string token = await CreateAccessTokenAsync(admin, formId, submissionId);
        using HttpClient anonymous = world.AnonymousClient();
        JsonElement byToken = await GetDefinitionAsync(
            anonymous, $"/api/forms/{formId}/submissions/by-access-token/{token}");

        foreach (JsonElement definition in new[] { byId, byToken })
        {
            Assert.False(definition.GetProperty("pdfUsesFormTheme").GetBoolean());
            Assert.Equal(JsonValueKind.Null, definition.GetProperty("themeModel").ValueKind);
        }
    }

    private static async Task<string> CreatePrivateFormWithThemeAsync(HttpClient admin)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpResponseMessage themeResponse = await admin.PostAsJsonAsync(
            "/api/themes", new { name = $"pdf-theme-{Guid.NewGuid():N}", jsonData = ThemeJson }, ct);
        themeResponse.EnsureSuccessStatusCode();
        string themeId = (await ReadJsonAsync(themeResponse)).GetProperty("id").GetString()!;

        using HttpResponseMessage formResponse = await admin.PostAsJsonAsync(
            "/api/forms",
            new { name = $"pdf-theme-form-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FormJson },
            ct);
        formResponse.EnsureSuccessStatusCode();
        string formId = (await ReadJsonAsync(formResponse)).GetProperty("id").GetString()!;

        JsonElement form = await PatchFormAsync(admin, formId, new { themeId = long.Parse(themeId) });
        Assert.False(form.GetProperty("isPublic").GetBoolean());
        Assert.False(form.GetProperty("pdfUsesFormTheme").GetBoolean());
        return formId;
    }

    private static async Task<JsonElement> PatchFormAsync(HttpClient admin, string formId, object changes)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await admin.PatchAsJsonAsync($"/api/forms/{formId}", changes, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<string> CreateSubmissionAsync(HttpClient admin, string formId)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = """{"q1":"answer"}""" },
            ct);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateAccessTokenAsync(
        HttpClient admin, string formId, string submissionId)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions = new[] { "view" } },
            ct);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> GetDefinitionAsync(HttpClient client, string url) =>
        (await GetJsonAsync(client, url)).GetProperty("formDefinition");

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpResponseMessage response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }
}
