using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSwag.AspNetCore;

namespace Endatix.Api.Tests.Setup;

/// <summary>
/// The API docs page shows Scalar over the document FastEndpoints generates, or Swagger UI when a host asks for it
/// (endatix/endatix#1846).
/// </summary>
[Collection(nameof(ApiDocsHostCollection))]
public sealed partial class ApiDocsTests
{
    private const string DOCUMENT_PATH = "/swagger/alpha-version/swagger.json";
    private const string DOCUMENT_URL = "swagger/alpha-version/swagger.json";
    private const string SWAGGER_UI_PAGE = "/swagger/index.html";

    [Fact]
    public async Task Docs_page_shows_Scalar_over_the_existing_document()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        page.DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, page.ResolveDocument())).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Docs_page_without_a_trailing_slash_redirects_to_Scalar()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/api-docs", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("api-docs/");
    }

    [Fact]
    public async Task Existing_document_path_still_serves_the_document_with_the_bearer_scheme()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        using var document = JsonDocument.Parse(
            await client.GetStringAsync(new Uri(DOCUMENT_PATH, UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        var root = document.RootElement;
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Endatix Platform REST API");
        root.GetProperty("paths").TryGetProperty("/api/ping", out _).Should().BeTrue();
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty(ApiDocs.BEARER_SCHEME);
        scheme.GetProperty("scheme").GetString().Should().Be("Bearer");
    }

    [Fact]
    public async Task Scalar_selects_the_bearer_scheme_with_no_token()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        var authentication = page.Configuration.GetProperty("authentication");
        authentication.GetProperty("preferredSecurityScheme").EnumerateArray().Select(s => s.GetString())
            .Should().Equal(ApiDocs.BEARER_SCHEME);
        authentication.TryGetProperty("securitySchemes", out _).Should().BeFalse();
        page.Html.Should().NotContain("\"token\"");
    }

    [Theory]
    [InlineData("/docs", "/docs/")]
    [InlineData("/docs/", "/docs/")]
    [InlineData(null, "/api-docs/")]
    [InlineData("", "/api-docs/")]
    [InlineData("/", "/api-docs/")]
    public async Task Docs_page_serves_at_the_docs_path(string? docsPath, string page)
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options => options.Docs.Path = docsPath);
        using var client = host.CreateClient();

        // Act
        var scalar = await ScalarPage.ReadAsync(client, page);

        // Assert
        scalar.DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/anything")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Docs_page_finds_the_document_under_a_path_base()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(beforeApi: app => app.UsePathBase("/sub"));
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/sub/api-docs/");

        // Assert
        page.ResolveDocument().Should().Be("/sub" + DOCUMENT_PATH);
        (await GetStatusAsync(client, page.ResolveDocument())).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Docs_page_under_a_path_base_redirects_to_its_trailing_slash()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(beforeApi: app => app.UsePathBase("/sub"));
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/sub/api-docs", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("api-docs/");
    }

    [Fact]
    public async Task Scalar_follows_a_document_path_the_host_moves()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options =>
            options.ConfigureOpenApiDocument = settings => settings.Path = "/openapi/{documentName}.json");
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        page.DocumentUrls.Should().Equal("openapi/alpha-version.json");
        (await GetStatusAsync(client, page.ResolveDocument())).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scalar_follows_a_document_path_the_host_moves_through_its_settings()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, _) => app.UseEndatixApi(options =>
            {
                options.UseExceptionHandler = false;
                options.UseCors = false;
            }),
            services => services.Configure<OpenApiDocumentMiddlewareSettings>(
                settings => settings.Path = "/openapi/{documentName}.json"));
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        page.DocumentUrls.Should().Equal("openapi/alpha-version.json");
        (await GetStatusAsync(client, page.ResolveDocument())).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scalar_calls_no_third_party()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        page.Configuration.GetProperty("telemetry").GetBoolean().Should().BeFalse();
        page.Configuration.GetProperty("withDefaultFonts").GetBoolean().Should().BeFalse();
        page.Configuration.GetProperty("agent").GetProperty("disabled").GetBoolean().Should().BeTrue(page.Html);
        page.Configuration.GetProperty("mcp").GetProperty("disabled").GetBoolean().Should().BeTrue();
        page.Configuration.GetProperty("showDeveloperTools").GetString().Should().Be("never");
        page.Configuration.GetProperty("hideClientButton").GetBoolean().Should().BeTrue();
    }

    private static async Task<HttpStatusCode> GetStatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>The Scalar page the server renders: the path it was asked for and the configuration it passes.</summary>
    private sealed partial record ScalarPage(string Html, string BrowserPath, string RenderedPath, JsonElement Configuration)
    {
        public IReadOnlyList<string?> DocumentUrls =>
            Configuration.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("url").GetString()).ToList();

        public static async Task<ScalarPage> ReadAsync(HttpClient client, string path)
        {
            var html = await client.GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            html.Should().Contain("scalar.js");
            var match = InitializeCall().Match(html);
            match.Success.Should().BeTrue(html);
            using var configuration = JsonDocument.Parse(match.Groups["config"].Value);
            return new ScalarPage(
                html, path, Uri.UnescapeDataString(match.Groups["path"].Value), configuration.RootElement.Clone());
        }

        /// <summary>
        /// Resolves the document URL the way Scalar's page script does: the path base is the browser path without the
        /// path the server rendered, and a relative URL resolves under it.
        /// </summary>
        public string ResolveDocument()
        {
            var basePath = BrowserPath.EndsWith(RenderedPath, StringComparison.Ordinal)
                ? BrowserPath[..^RenderedPath.Length]
                : string.Empty;
            return new Uri(new Uri($"http://localhost{basePath}/"), DocumentUrls.Single()).AbsolutePath;
        }

        [GeneratedRegex(@"initialize\(\s*'(?<path>[^']*)',\s*\w+,\s*(?<config>\{.*\}),\s*'", RegexOptions.Singleline)]
        private static partial Regex InitializeCall();
    }
}
