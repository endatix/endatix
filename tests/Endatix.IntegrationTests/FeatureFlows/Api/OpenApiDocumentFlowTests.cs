using System.Net;
using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.Api;

/// <summary>
/// The OpenAPI document behind the API docs page generates for every endpoint (endatix/endatix#1841).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class OpenApiDocumentFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string DocumentPath = "/swagger/alpha-version/swagger.json";

    [Fact]
    public async Task Document_generates_for_every_endpoint()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.Factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(DocumentPath, UriKind.Relative), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
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
        using var client = fixture.Factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(new Uri(DocumentPath, UriKind.Relative), ct));
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
