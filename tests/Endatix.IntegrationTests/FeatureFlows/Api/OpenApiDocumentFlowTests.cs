using System.Net;
using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.Api;

/// <summary>
/// The OpenAPI document behind the API docs page generates for every endpoint (endatix/endatix#1841), with WhiteLabeling
/// off (the shared host) and on.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class OpenApiDocumentFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string DOCUMENT_PATH = "/swagger/alpha-version/swagger.json";
    private static readonly string[] _objectFields = ["assets", "tokens"];

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
    public async Task Document_generates_for_every_endpoint_when_white_labeling_is_on()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using BrandingWebHost host = new(fixture);
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(DOCUMENT_PATH, UriKind.Relative), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// The publish body is read as text, so its schema is a documentation type. It lists the fields the brand override
    /// parser accepts at its root, and not the request's raw <c>content</c>. Only <c>version</c> is required, and
    /// <c>assets</c> and <c>tokens</c> are objects that are not nullable, since the parser refuses a null one.
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
        var body = Resolve(root, schema);
        var properties = body.GetProperty("properties");
        var required = body.TryGetProperty("required", out var names)
            ? names.EnumerateArray().Select(name => name.GetString()).ToList()
            : [];

        // Assert
        properties.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(BrandOverrideReader.ROOT_FIELDS);
        required.Should().Equal("version");
        foreach (var name in _objectFields)
        {
            var field = Resolve(root, Unwrap(properties.GetProperty(name)));
            var raw = properties.GetProperty(name).GetRawText();
            field.GetProperty("type").GetString().Should().Be("object", raw);
            field.GetProperty("additionalProperties").ValueKind.Should().NotBe(JsonValueKind.False, raw);
            (properties.GetProperty(name).TryGetProperty("nullable", out var nullable) && nullable.GetBoolean())
                .Should().BeFalse(raw);
        }
    }

    private static async Task<JsonDocument> ReadDocumentAsync(HttpClient client, CancellationToken ct) =>
        JsonDocument.Parse(await client.GetStringAsync(new Uri(DOCUMENT_PATH, UriKind.Relative), ct));

    /// <summary>NSwag writes a reference beside other keywords as a single <c>allOf</c> or <c>oneOf</c>.</summary>
    private static JsonElement Unwrap(JsonElement schema)
    {
        foreach (var keyword in (string[])["allOf", "oneOf"])
        {
            if (schema.TryGetProperty(keyword, out var options) && options.GetArrayLength() == 1)
            {
                return options[0];
            }
        }

        return schema;
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
