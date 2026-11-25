using System.Net;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Setup;

/// <summary>
/// Hosts that change nothing in code keep working: every docs configuration a host may have in appsettings.json starts
/// without a throw and serves what this release promises (endatix/endatix#1846).
/// </summary>
public sealed partial class ApiDocsTests
{
    private static readonly Dictionary<string, CompatibilityRow> _compatibilityRows = new()
    {
        ["no keys"] = new() { Settings = [], DocsPage = "/api-docs", BookmarkTarget = SWAGGER_UI_PAGE },
        ["SwaggerPath /x"] = new() { Settings = Keys(("SwaggerPath", "/x")), DocsPage = "/x", BookmarkTarget = SWAGGER_UI_PAGE },
        ["SwaggerPath empty"] = new()
        {
            Settings = Keys(("SwaggerPath", "")),
            DocsPage = "/api-docs",
            BookmarkTarget = SWAGGER_UI_PAGE,
            RootTarget = "/api-docs/",
            Warning = "Endatix:Api:SwaggerPath ''",
        },
        ["SwaggerPath /swagger"] = new()
        {
            Settings = Keys(("SwaggerPath", "/swagger")),
            DocsPage = "/api-docs",
            BookmarkTarget = SWAGGER_UI_PAGE,
            Warning = "both asked for /swagger",
        },
        ["UseSwagger false"] = new() { Settings = Keys(("UseSwagger", "false")) },
        ["SwaggerUi Enabled false"] = new()
        {
            Settings = Keys(("Docs:SwaggerUi:Enabled", "false")),
            DocsPage = "/api-docs",
            BookmarkTarget = "/api-docs/",
            SwaggerUi = HttpStatusCode.NotFound,
        },
        ["Docs Path wins over SwaggerPath"] = new()
        {
            Settings = Keys(("Docs:Path", "/new"), ("SwaggerPath", "/old")),
            DocsPage = "/new",
            BookmarkTarget = SWAGGER_UI_PAGE,
            Warning = "Endatix:Api:Docs:Path '/new' and the deprecated Endatix:Api:SwaggerPath '/old'",
        },
        ["Docs Enabled wins over UseSwagger"] = new()
        {
            Settings = Keys(("Docs:Enabled", "false"), ("UseSwagger", "true")),
            Warning = "Endatix:Api:Docs:Enabled 'False' and the deprecated Endatix:Api:UseSwagger 'True'",
        },
        ["SwaggerUi Path on the docs page"] = new()
        {
            Settings = Keys(("Docs:SwaggerUi:Path", "/api-docs")),
            DocsPage = "/scalar",
            BookmarkTarget = "/api-docs/index.html",
            SwaggerUi = HttpStatusCode.NotFound,
            Warning = "both asked for /api-docs",
        },
    };

    [Theory]
    [InlineData("no keys")]
    [InlineData("SwaggerPath /x")]
    [InlineData("SwaggerPath empty")]
    [InlineData("SwaggerPath /swagger")]
    [InlineData("UseSwagger false")]
    [InlineData("SwaggerUi Enabled false")]
    [InlineData("Docs Path wins over SwaggerPath")]
    [InlineData("Docs Enabled wins over UseSwagger")]
    [InlineData("SwaggerUi Path on the docs page")]
    public async Task Configured_host_starts_and_serves_what_its_settings_promise(string rowName)
    {
        // Arrange
        var row = _compatibilityRows[rowName];
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(row.Settings, log: log);
        using var client = host.CreateClient();

        // Assert
        await AssertDocsPageAsync(client, row);
        await AssertRedirectAsync(client, $"{row.DocsPage ?? "/api-docs"}/index.html", row.BookmarkTarget);
        (await GetStatusAsync(client, SWAGGER_UI_PAGE)).Should().Be(row.DocsPage is null ? HttpStatusCode.NotFound : row.SwaggerUi);
        (await GetStatusAsync(client, DOCUMENT_PATH)).Should().Be(row.DocsPage is null ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        await AssertRedirectAsync(client, "/", row.RootTarget);
        AssertWarnings(log, row.Warning);
    }

    [Fact]
    public async Task Customer_host_keeps_its_controller_root_under_a_path_base_with_an_empty_swagger_path()
    {
        // Arrange
        await using var host = await ApiDocsHost.StartFromConfigWithServicesAsync(
            Keys(("SwaggerPath", "")),
            app =>
            {
                app.UsePathBase("/api");
                app.MapControllers();
            },
            services => services.AddControllers().AddApplicationPart(typeof(CustomerRootController).Assembly));
        using var client = host.CreateClient();

        // Act
        using var root = await client.GetAsync(new Uri("/api/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        root.StatusCode.Should().Be(HttpStatusCode.OK);
        (await root.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(CustomerRootController.BODY);
        (await ScalarPage.ReadAsync(client, "/api/api-docs/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/api" + SWAGGER_UI_PAGE)).Should().Be(HttpStatusCode.OK);
        await AssertRedirectAsync(client, "/api/api-docs/index.html", "/api" + SWAGGER_UI_PAGE);
    }

    [Fact]
    public async Task Docs_added_first_with_an_explicit_path_log_no_conflict_for_the_path_they_replaced()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(
            Keys(("Docs:Path", "/new"), ("SwaggerPath", "/old")),
            beforeApi: app => app.UseEndatixApiDocs(new ApiOptions { Docs = ConfiguredDocs(app).ForExplicitDocs("/given", false) }),
            log: log);
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/given/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Warnings.Should().NotContain(line => line.Contains("Endatix:Api:Docs:Path", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unset_swagger_ui_setting_logs_that_its_default_changes_next_release()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync([], log: log);

        // Assert
        log.Information.Should().ContainSingle(line => line.Contains("off by default from the next release", StringComparison.Ordinal)
            && line.Contains("Endatix:Api:Docs:SwaggerUi:Enabled", StringComparison.Ordinal));
        log.Information.Should().Contain("Serving Swagger UI at /swagger");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task Explicit_swagger_ui_setting_logs_no_default_change(string enabled)
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(Keys(("Docs:SwaggerUi:Enabled", enabled)), log: log);

        // Assert
        log.Information.Should().NotContain(line => line.Contains("next release", StringComparison.Ordinal));
    }

    private static async Task AssertDocsPageAsync(HttpClient client, CompatibilityRow row)
    {
        if (row.DocsPage is null)
        {
            (await GetStatusAsync(client, "/api-docs/")).Should().Be(HttpStatusCode.NotFound);
            return;
        }

        (await ScalarPage.ReadAsync(client, $"{row.DocsPage}/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
    }

    /// <summary>A redirect to the target, resolved as a browser resolves it; a null target means not found.</summary>
    private static async Task AssertRedirectAsync(HttpClient client, string path, string? target)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        if (target is null)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, path);
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, path);
        new Uri(new Uri("http://localhost" + path), response.Headers.Location!).AbsolutePath.Should().Be(target, path);
    }

    private static void AssertWarnings(WarningLog log, string? warning)
    {
        if (warning is null)
        {
            log.Warnings.Should().BeEmpty();
            return;
        }

        log.Warnings.Should().ContainSingle(line => line.Contains(warning, StringComparison.Ordinal));
    }

    private static ApiDocsOptions ConfiguredDocs(WebApplication app) =>
        app.Services.GetRequiredService<IOptions<ApiOptions>>().Value.Docs;

    private static Dictionary<string, string?> Keys(params (string Key, string? Value)[] settings) =>
        settings.ToDictionary(setting => $"Endatix:Api:{setting.Key}", setting => setting.Value);

    /// <summary>
    /// One configuration and what it serves. A null docs page means the docs are off, so every docs URL is not found. A
    /// null redirect target means that URL is not found.
    /// </summary>
    private sealed class CompatibilityRow
    {
        public required Dictionary<string, string?> Settings { get; init; }

        public string? DocsPage { get; init; }

        public string? BookmarkTarget { get; init; }

        public HttpStatusCode SwaggerUi { get; init; } = HttpStatusCode.OK;

        public string? RootTarget { get; init; }

        public string? Warning { get; init; }
    }
}
