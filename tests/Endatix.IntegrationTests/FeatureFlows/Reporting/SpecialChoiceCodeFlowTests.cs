using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SpecialChoiceCodeFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task Changing_a_formats_code_to_an_occupied_category_returns_a_problem_before_download()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        var formatId = await CreateCodebookFormatAsync(client, ct);
        var export = $"/api/forms/{formId}/submissions/export";
        using var original = await client.PostAsJsonAsync(export, new { exportFormatId = formatId }, ct);
        original.EnsureSuccessStatusCode();
        var book = await original.Content.ReadFromJsonAsync<JsonElement>(ct);
        book.GetProperty("choiceCatalogs").GetProperty("q").GetProperty("choices")[1].GetProperty("id").GetInt32().Should().Be(99);

        // Act
        using var update = await client.PatchAsJsonAsync($"/api/settings/export-formats/{formatId}",
            new { settings = new { specialChoiceCodes = new { none = 1 } } }, ct);
        update.EnsureSuccessStatusCode();
        using var rejected = await client.PostAsJsonAsync(export, new { exportFormatId = formatId }, ct);

        // Assert
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        rejected.Content.Headers.ContentDisposition.Should().BeNull();
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("detail").GetString().Should().Contain("id of 'a' in question 'q'");
    }

    private static async Task<string> CreateCompiledFormAsync(HttpClient client, CancellationToken ct)
    {
        const string definition = """{"pages":[{"elements":[{"name":"q","type":"dropdown","choices":["a"],"showNoneItem":true}]}]}""";
        using var form = await client.PostAsJsonAsync("/api/forms",
            new { name = "Special choices", isEnabled = true, formDefinitionJsonData = definition }, ct);
        form.EnsureSuccessStatusCode();
        var body = await form.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = body.GetProperty("id").GetString()!;
        using var compile = await client.PostAsJsonAsync($"/api/forms/{id}/reporting/compile-schema", new { }, ct);
        compile.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<string> CreateCodebookFormatAsync(HttpClient client, CancellationToken ct)
    {
        using var format = await client.PostAsJsonAsync("/api/settings/export-formats", new
        {
            name = "Coded metadata",
            exportTarget = "Codebook",
            deliveryFormat = "Json",
            settings = new { specialChoiceCodes = new { none = 99 } },
        }, ct);
        format.EnsureSuccessStatusCode();
        var body = await format.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("id").GetString()!;
    }
}
