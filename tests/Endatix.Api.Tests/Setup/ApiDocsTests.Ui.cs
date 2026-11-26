using System.Net;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Tests.Setup;

/// <summary>Which UI the docs page shows, and the pages a host that adds the docs more than once gets.</summary>
public sealed partial class ApiDocsTests
{
    [Theory]
    [InlineData("/api-docs/index.html", "/api-docs/")]
    [InlineData("/sub/api-docs/index.html", "/sub/api-docs/")]
    public async Task Index_html_under_the_Scalar_page_redirects_to_the_page_keeping_the_path_base(string index, string page)
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(beforeApi: app => app.UsePathBase("/sub"));
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri(index, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("./");
        new Uri(new Uri("http://localhost" + index), response.Headers.Location).AbsolutePath.Should().Be(page);
    }

    [Fact]
    public async Task Swagger_ui_is_off_by_default()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync();
        using var client = host.CreateClient();

        // Act
        var status = await GetStatusAsync(client, SWAGGER_UI_PAGE);

        // Assert
        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ui_swagger_ui_shows_swagger_ui_at_the_docs_page_instead_of_Scalar()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options => options.Docs.Ui = ApiDocsUi.Swagger);
        using var client = host.CreateClient();

        // Act
        var html = await client.GetStringAsync(new Uri("/api-docs/index.html", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        html.Should().Contain("swagger-ui").And.NotContain("scalar.js");
        (await GetStatusAsync(client, DOCUMENT_PATH)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_ui_settings_in_code_without_a_ui_show_swagger_ui_at_the_docs_page()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options => options.ConfigureSwaggerUi = ui => ui.DocExpansion = "list");
        using var client = host.CreateClient();

        // Act
        var html = await client.GetStringAsync(new Uri("/api-docs/index.html", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        html.Should().Contain("swagger-ui");
        (await GetStatusAsync(client, SWAGGER_UI_PAGE)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Swagger_ui_settings_in_code_with_ui_Scalar_show_Scalar_and_warn_they_are_unused()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(
            new ApiOptions { Docs = { Ui = ApiDocsUi.Scalar }, ConfigureSwaggerUi = ui => ui.DocExpansion = "list" }, log);
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/sub/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Warnings.Should().ContainSingle()
            .Which.Should().Be("ConfigureSwaggerUi is set, but Endatix:Api:Docs:Ui is Scalar, so the Swagger UI settings are unused.");
    }

    [Fact]
    public async Task Swagger_ui_settings_that_move_its_path_serve_it_there()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { ConfigureSwaggerUi = ui => ui.Path = "/ui" }, log);
        using var client = host.CreateClient();

        // Assert
        (await GetStatusAsync(client, "/sub/ui/index.html")).Should().Be(HttpStatusCode.OK);
        (await GetStatusAsync(client, "/sub/api-docs/index.html")).Should().Be(HttpStatusCode.NotFound);
        log.Information.Should().Contain("Serving the API docs at /ui with Swagger");
    }

    [Fact]
    public async Task Ui_none_serves_only_the_document_and_no_root_redirect()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Ui = ApiDocsUi.None, Path = "" } }, log);
        using var client = host.CreateClient();

        // Assert
        (await GetStatusAsync(client, "/sub" + DOCUMENT_PATH)).Should().Be(HttpStatusCode.OK);
        (await GetStatusAsync(client, "/sub/api-docs/")).Should().Be(HttpStatusCode.NotFound);
        (await GetStatusAsync(client, "/sub/")).Should().Be(HttpStatusCode.NotFound);
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Use_swagger_false_turns_off_the_document_and_every_ui()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options =>
        {
            options.UseSwagger = false;
            options.Docs.Ui = ApiDocsUi.Swagger;
        });
        using var client = host.CreateClient();

        // Act
        var statuses = new[] { DOCUMENT_PATH, "/api-docs/", "/api-docs/index.html", SWAGGER_UI_PAGE }
            .Select(path => GetStatusAsync(client, path));

        // Assert
        (await Task.WhenAll(statuses)).Should().AllBeEquivalentTo(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Legacy_swagger_path_set_in_code_serves_the_docs_page_there()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(options => options.SwaggerPath = "/legacy");
        using var client = host.CreateClient();

        // Act
        var page = await ScalarPage.ReadAsync(client, "/legacy/");

        // Assert
        page.DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/api-docs/")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_the_docs_twice_at_one_path_keeps_one_page_and_one_serving_line()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, _) =>
            {
                app.UseEndatixApiDocs(new ApiOptions());
                app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = "/api-docs/" } });
            },
            services => services.AddSingleton(log.Provider));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Information.Should().ContainSingle(line => line.StartsWith("Serving the API docs", StringComparison.Ordinal));
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Api_docs_added_before_the_api_at_another_path_serve_both_pages()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(
            beforeApi: app => app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = "/docs" } }));
        using var client = host.CreateClient();

        // Act
        var first = await ScalarPage.ReadAsync(client, "/docs/");
        var second = await ScalarPage.ReadAsync(client, "/api-docs/");

        // Assert
        first.DocumentUrls.Should().Equal(DOCUMENT_URL);
        second.DocumentUrls.Should().Equal(DOCUMENT_URL);
    }

    [Fact]
    public async Task Api_docs_added_after_the_api_at_another_path_add_a_page_over_the_same_document()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartAsync(afterApi: (app, _) => app.UseEndatixApiDocs(new ApiOptions
        {
            Docs = { Path = "/swagger-ui", Ui = ApiDocsUi.Swagger },
            ConfigureOpenApiDocument = settings => settings.Path = "/other/{documentName}.json",
        }));
        using var client = host.CreateClient();

        // Act
        var html = await client.GetStringAsync(new Uri("/swagger-ui/index.html", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        html.Should().Contain("swagger-ui");
        (await ScalarPage.ReadAsync(client, "/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/other/alpha-version.json")).Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// FastEndpoints refuses a second <c>UseFastEndpoints</c>, so a host cannot also call <c>UseEndatixApi</c> here; the
    /// docs call it would make is the second one.
    /// </summary>
    [Fact]
    public async Task Configure_application_and_the_api_docs_call_serve_one_docs_page()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, api) =>
            {
                api.ConfigureApplication(app);
                app.UseEndatixApiDocs(ApiApplicationBuilderExtensions.GetApiOptions(app));
            },
            services => services.AddSingleton(log.Provider));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Information.Should().ContainSingle(line => line.StartsWith("Serving the API docs", StringComparison.Ordinal));
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Paths_that_differ_only_in_slashes_map_one_page()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, _) =>
            {
                app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = "docs" } });
                app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = " /docs/ " } });
            },
            services => services.AddSingleton(log.Provider));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Information.Should().ContainSingle(line => line.StartsWith("Serving the API docs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Swagger_ui_settings_that_move_it_onto_a_mapped_page_skip_it_with_a_warning()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, _) =>
            {
                app.UseEndatixApiDocs(new ApiOptions());
                app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = "/other" }, ConfigureSwaggerUi = ui => ui.Path = "/api-docs" });
            },
            services => services.AddSingleton(log.Provider));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/api-docs/index.html")).Should().Be(HttpStatusCode.Redirect);
        log.Warnings.Should().ContainSingle()
            .Which.Should().Contain("move the API docs page to /api-docs, which already has a docs page");
    }

    [Fact]
    public async Task Swagger_ui_settings_that_move_it_to_the_root_serve_it_there_without_a_root_redirect()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(
            new ApiOptions { Docs = { Path = "" }, ConfigureSwaggerUi = ui => ui.Path = string.Empty }, log);
        using var client = host.CreateClient();

        // Assert
        (await client.GetStringAsync(new Uri("/sub/index.html", UriKind.Relative), TestContext.Current.CancellationToken))
            .Should().Contain("swagger-ui");
        log.Information.Should().Contain("Serving the API docs at / with Swagger");
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Undefined_ui_value_in_code_shows_Scalar_with_a_warning()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await StartWithWarningsAsync(new ApiOptions { Docs = { Ui = (ApiDocsUi)99 } }, log);
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/sub/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Warnings.Should().ContainSingle().Which.Should().StartWith("Endatix:Api:Docs:Ui '99' is not a docs UI.");
    }

    [Fact]
    public async Task Document_settings_of_a_later_call_are_ignored_with_a_warning()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartWithPipelineAsync(
            (app, _) =>
            {
                app.UseEndatixApiDocs(new ApiOptions());
                app.UseEndatixApiDocs(new ApiOptions
                {
                    Docs = { Path = "/x" },
                    ConfigureOpenApiDocument = settings => settings.Path = "/other/{documentName}.json",
                });
            },
            services => services.AddSingleton(log.Provider));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/x/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Warnings.Should().ContainSingle().Which.Should().Contain("ConfigureOpenApiDocument settings are ignored");
    }

    [Fact]
    public void Docs_without_endpoint_routing_skip_the_Scalar_page_with_a_warning()
    {
        // Arrange
        var log = new WarningLog();
        using var services = new ServiceCollection().AddLogging().AddSingleton(log.Provider).BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        // Act
        var add = () => app.UseEndatixApiDocs(new ApiOptions { Docs = { Path = "" } });

        // Assert
        add.Should().NotThrow();
        log.Warnings.Should().ContainSingle().Which.Should().Contain("/api-docs needs endpoint routing, so it is skipped");
    }
}
