using System.Net;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Setup;

/// <summary>Which path the docs page takes: ApiDocsPath, its deprecated mirror SwaggerPath, and the root fallback.</summary>
public sealed partial class ApiDocsTests
{
    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("  ", "/")]
    [InlineData("/", "/sub/")]
    public async Task Root_api_docs_path_redirects_the_root_to_the_docs_page_with_a_warning(string apiDocsPath, string root)
    {
        // Arrange
        var log = new WarningLog();
        await using var host = await StartWithWarningsAsync(new ApiOptions { ApiDocsPath = apiDocsPath }, log);
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(root, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be(root + "api-docs/");
        log.Warnings.Should().ContainSingle(warning => warning.Contains("ApiDocsPath", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/docs")]
    public async Task Set_or_unset_api_docs_path_adds_no_root_redirect_and_no_warning(string? apiDocsPath)
    {
        // Arrange
        var log = new WarningLog();
        await using var host = await StartWithWarningsAsync(new ApiOptions { ApiDocsPath = apiDocsPath }, log);
        using var client = host.CreateClient();

        // Act
        var status = await GetStatusAsync(client, "/");

        // Assert
        status.Should().Be(HttpStatusCode.NotFound);
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Deprecated_swagger_path_alone_still_moves_the_docs_page()
    {
        // Arrange
        var log = new WarningLog();
#pragma warning disable CS0618 // Subject under test: the deprecated mirror of ApiDocsPath.
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "/docs" }, log);
#pragma warning restore CS0618
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/docs/");

        // Assert
        page.DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/api-docs/")).Should().Be(HttpStatusCode.NotFound);
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Deprecated_root_swagger_path_redirects_the_root_to_the_docs_page()
    {
        // Arrange
        var log = new WarningLog();
#pragma warning disable CS0618 // Subject under test: the deprecated mirror of ApiDocsPath.
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "/" }, log);
#pragma warning restore CS0618
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.Headers.Location!.OriginalString.Should().Be("/api-docs/");
        log.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task Root_fallback_warning_names_api_docs_path_when_it_set_the_root()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { ApiDocsPath = "" }, log);

        // Assert
        log.Warnings.Should().ContainSingle(warning => warning.StartsWith("ApiDocsPath ''", StringComparison.Ordinal)
            && !warning.Contains("SwaggerPath", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Root_fallback_warning_names_swagger_path_and_points_to_api_docs_path_when_it_set_the_root()
    {
        // Arrange
        var log = new WarningLog();

        // Act
#pragma warning disable CS0618 // Subject under test: the deprecated mirror of ApiDocsPath.
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "" }, log);
#pragma warning restore CS0618

        // Assert
        log.Warnings.Should().ContainSingle(warning => warning.StartsWith("SwaggerPath ''", StringComparison.Ordinal)
            && warning.Contains("set ApiDocsPath instead", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Api_docs_path_wins_over_a_different_swagger_path_with_a_warning()
    {
        // Arrange
        var log = new WarningLog();
#pragma warning disable CS0618 // Subject under test: the deprecated mirror loses to ApiDocsPath.
        var options = new ApiOptions { ApiDocsPath = "/docs", SwaggerPath = "/old-docs" };
#pragma warning restore CS0618
        await using var host = await StartWithWarningsAsync(options, log);
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/docs/");

        // Assert
        page.DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/old-docs/")).Should().Be(HttpStatusCode.NotFound);
        log.Warnings.Should().ContainSingle(warning => warning.Contains("'/docs'", StringComparison.Ordinal)
            && warning.Contains("'/old-docs'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, null, "/api-docs")]
    [InlineData("", null, "/api-docs")]
    [InlineData(null, "/old-docs/", "/old-docs")]
    [InlineData("/docs", "/old-docs", "/docs")]
    public void Resolved_page_path_is_the_page_the_docs_serve_at(string? apiDocsPath, string? swaggerPath, string expected)
    {
        // Arrange
#pragma warning disable CS0618 // Subject under test: the deprecated mirror of ApiDocsPath.
        var options = new ApiOptions { ApiDocsPath = apiDocsPath, SwaggerPath = swaggerPath };
#pragma warning restore CS0618

        // Act
        var pagePath = ApiDocs.ResolvedPagePath(options);

        // Assert
        pagePath.Should().Be(expected);
    }

    private static Task<ApiDocsHost> StartWithWarningsAsync(ApiOptions options, WarningLog log) =>
        ApiDocsHost.StartWithPipelineAsync(
            (app, _) =>
            {
                app.UsePathBase("/sub");
                app.UseEndatixApiDocs(options);
            },
            services => services.AddSingleton(log.Provider));
}
