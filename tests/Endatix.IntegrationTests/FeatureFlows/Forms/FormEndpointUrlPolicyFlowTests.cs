using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

/// <summary>
/// A form's webhook endpoint URLs, saved over HTTP by a form editor, must name a public network address unless the
/// server allows private network targets.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class FormEndpointUrlPolicyFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string Definition = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""";

    public static TheoryData<string> PrivateEndpointUrls() =>
    [
        "http://169.254.10.20/hooks",
        "http://127.0.0.1:8080/hooks",
        "http://localhost/admin",
        "http://[::1]/",
        "http://10.0.0.5/",
        "http://192.168.1.10/",
        "http://[::ffff:169.254.10.20]/",
    ];

    [Theory]
    [MemberData(nameof(PrivateEndpointUrls))]
    public async Task Patch_with_private_endpoint_url_returns_400(string url)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);
        var formId = await CreateFormAsync(editor, ct);

        // Act
        using var response = await editor.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { webHookSettingsJson = Settings(url) }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("ftp://files.example.com/drop")]
    [InlineData("/relative/path")]
    public async Task Patch_with_an_endpoint_url_that_is_not_absolute_http_returns_400(string url)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);
        var formId = await CreateFormAsync(editor, ct);

        // Act
        using var response = await editor.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { webHookSettingsJson = Settings(url) }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("must be an absolute http or https URL");
    }

    [Fact]
    public async Task Refused_endpoint_url_is_not_stored()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);
        var formId = await CreateFormAsync(editor, ct);
        const string url = "http://169.254.10.20/hooks";

        // Act
        using var patched = await editor.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { webHookSettingsJson = Settings(url) }, ct);
        using var read = await editor.GetAsync($"/api/forms/{formId}", ct);

        // Assert
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsStringAsync(ct)).Should().NotContain("169.254.10.20");
    }

    [Fact]
    public async Task Put_with_private_endpoint_url_returns_400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);
        var formId = await CreateFormAsync(editor, ct);

        // Act
        using var response = await editor.PutAsJsonAsync(
            $"/api/forms/{formId}",
            new { name = "endpoint-policy", isEnabled = true, webHookSettingsJson = Settings("http://127.0.0.1:8080/") },
            ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_private_endpoint_url_in_the_typed_field_returns_400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);

        // Act
        using var response = await editor.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"endpoint-policy-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = Definition,
                webHookSettings = JsonDocument.Parse(Settings("http://169.254.10.20/")).RootElement,
            },
            ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_private_endpoint_url_in_the_string_field_returns_400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);

        // Act
        using var response = await editor.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"endpoint-policy-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = Definition,
                webHookSettingsJson = Settings("http://10.0.0.5/"),
            },
            ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // A public endpoint is still saved: the rule refuses only private addresses.
    [Fact]
    public async Task Patch_with_public_endpoint_url_is_saved()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var editor = await FormEditorAsync(ct);
        var formId = await CreateFormAsync(editor, ct);

        // Act
        using var response = await editor.PatchAsJsonAsync(
            $"/api/forms/{formId}", new { webHookSettingsJson = Settings("https://hooks.example.com/endatix") }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> FormEditorAsync(CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.Creator, cancellationToken: ct);
    }

    private static async Task<long> CreateFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"endpoint-policy-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return long.Parse(body.RootElement.GetProperty("id").GetString()!);
    }

    private static string Settings(string url) =>
        JsonSerializer.Serialize(new
        {
            Events = new
            {
                SubmissionCompleted = new { IsEnabled = true, WebHookEndpoints = new[] { new { Url = url } } },
            },
        });
}
