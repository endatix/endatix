using System.Net;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Setup;

/// <summary>Which path the docs page takes: Docs.Path, its deprecated mirror SwaggerPath, and the root fallback.</summary>
public sealed partial class ApiDocsTests
{
    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("  ", "/")]
    [InlineData("/", "/sub/")]
    public async Task Root_docs_path_redirects_the_root_to_the_docs_page_with_a_warning(string docsPath, string root)
    {
        // Arrange
        var log = new WarningLog();
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Path = docsPath } }, log);
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(root, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be(root + "api-docs/");
        log.Warnings.Should().ContainSingle(warning => warning.StartsWith("Endatix:Api:Docs:Path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/docs")]
    public async Task Set_or_unset_docs_path_adds_no_root_redirect_and_no_warning(string? docsPath)
    {
        // Arrange
        var log = new WarningLog();
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Path = docsPath } }, log);
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
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "/docs" }, log);
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
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "/" }, log);
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.Headers.Location!.OriginalString.Should().Be("/api-docs/");
        log.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task Root_fallback_warning_names_docs_path_when_it_set_the_root()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Path = "" } }, log);

        // Assert
        log.Warnings.Should().ContainSingle(warning => warning.StartsWith("Endatix:Api:Docs:Path ''", StringComparison.Ordinal)
            && !warning.Contains("SwaggerPath", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Root_fallback_warning_names_swagger_path_and_points_to_docs_path_when_it_set_the_root()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { SwaggerPath = "" }, log);

        // Assert
        log.Warnings.Should().ContainSingle(warning => warning.StartsWith("Endatix:Api:SwaggerPath ''", StringComparison.Ordinal)
            && warning.Contains("set Endatix:Api:Docs:Path instead", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/docs", "/docs")]
    [InlineData("/", "/api-docs")]
    public async Task Adding_the_docs_logs_the_page_they_serve_at(string docsPath, string expected)
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Path = docsPath } }, log);

        // Assert
        log.Information.Should().ContainSingle(line => line == $"Serving the API docs at {expected} with Scalar");
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
