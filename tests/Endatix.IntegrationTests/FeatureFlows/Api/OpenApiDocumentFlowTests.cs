using System.Net;
using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.Api;

/// <summary>
/// The OpenAPI document behind the API docs page generates for every endpoint (endatix/endatix#1841), and lists the tenant
/// branding endpoints only when WhiteLabeling is on (endatix/endatix#1843). The shared host has the flag off.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class OpenApiDocumentFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string DOCUMENT_PATH = "/swagger/alpha-version/swagger.json";
    private const string BRANDING_ROUTE = "/tenant-branding";

    [Fact]
    public async Task Document_generates_for_every_endpoint()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(DOCUMENT_PATH, UriKind.Relative), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Branding_endpoints_are_not_documented_when_white_labeling_is_off()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        using var document = await ReadDocumentAsync(client, ct);

        // Act
        var paths = Paths(document);

        // Assert
        paths.Should().NotContain(path => path.Contains(BRANDING_ROUTE, StringComparison.Ordinal));
        paths.Should().Contain(path => path.EndsWith("/public/tenants/{slug}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Branding_endpoints_are_documented_when_white_labeling_is_on()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using BrandingWebHost host = new(fixture);
        using var client = host.CreateClient();

        using var document = await ReadDocumentAsync(client, ct);

        // Act
        var paths = Paths(document);
        var brandingTags = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.Contains(BRANDING_ROUTE, StringComparison.Ordinal))
            .SelectMany(path => path.Value.EnumerateObject())
            .SelectMany(operation => operation.Value.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()))
            .Distinct()
            .ToList();

        // Assert
        paths.Where(path => path.Contains(BRANDING_ROUTE, StringComparison.Ordinal))
            .Select(path => path[path.IndexOf(BRANDING_ROUTE, StringComparison.Ordinal)..])
            .Should().BeEquivalentTo(
                BRANDING_ROUTE, "/tenant-branding/versions", "/tenant-branding/published", "/tenant-branding/restore",
                "/tenant-branding/rollback", "/tenant-branding/reset");
        brandingTags.Should().Equal("Tenant-Branding");
        paths.Should().Contain(path => path.EndsWith("/public/tenants/{slug}", StringComparison.Ordinal));
    }

    /// <summary>
    /// The publish body is read as text, so its schema is a documentation type. It lists the fields the brand override
    /// parser accepts at its root, and not the request's raw <c>content</c>.
    /// </summary>
    [Fact]
    public async Task Publish_branding_body_is_json_in_the_brand_override_shape()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using BrandingWebHost host = new(fixture);
        using var client = host.CreateClient();
        using var document = await ReadDocumentAsync(client, ct);
        var root = document.RootElement;

        // Act
        var publish = root.GetProperty("paths").EnumerateObject()
            .Single(path => path.Name.EndsWith("/tenant-branding/published", StringComparison.Ordinal))
            .Value.GetProperty("put");
        var schema = publish.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        var properties = Resolve(root, schema).GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();

        // Assert
        properties.Should().BeEquivalentTo(BrandOverrideReader.ROOT_FIELDS);
    }

    private static async Task<JsonDocument> ReadDocumentAsync(HttpClient client, CancellationToken ct) =>
        JsonDocument.Parse(await client.GetStringAsync(new Uri(DOCUMENT_PATH, UriKind.Relative), ct));

    private static List<string> Paths(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();

    private static JsonElement Resolve(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var name = reference.GetString()!.Split('/')[^1];
        return root.GetProperty("components").GetProperty("schemas").GetProperty(name);
    }
}
