using System.Net;
using Endatix.Api.Builders;
using Endatix.Api.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Setup;

/// <summary>
/// Hosts that change nothing in code keep working: every docs configuration a host may have in appsettings.json starts
/// without a throw and serves what its settings promise (endatix/endatix#1846).
/// </summary>
public sealed partial class ApiDocsTests
{
    private static readonly Served _scalarAtApiDocs = new()
    {
        ApiDocs = Status.Redirect("api-docs/"),
        ApiDocsSlash = Status.Ok,
        ApiDocsIndex = Status.Redirect("./"),
        ScalarPage = "/api-docs/",
    };

    private static readonly Served _nothingAtApiDocs = new()
    {
        ApiDocs = Status.NotFound,
        ApiDocsSlash = Status.NotFound,
        ApiDocsIndex = Status.NotFound,
    };

    private static readonly Served _swaggerUiAtApiDocs = new()
    {
        ApiDocs = Status.Redirect("/api-docs/index.html"),
        ApiDocsSlash = Status.Redirect("/api-docs/index.html"),
        ApiDocsIndex = Status.Ok,
        SwaggerUiPage = "/api-docs/index.html",
    };

    private static readonly Dictionary<string, CompatibilityRow> _compatibilityRows = new()
    {
        ["no keys"] = new() { Settings = [], Served = _scalarAtApiDocs },
        ["Docs Ui SwaggerUi"] = new() { Settings = Keys(("Docs:Ui", "SwaggerUi")), Served = _swaggerUiAtApiDocs },
        ["Docs Ui swagger alias"] = new() { Settings = Keys(("Docs:Ui", "swagger")), Served = _swaggerUiAtApiDocs },
        ["Docs Ui Bogus"] = new()
        {
            Settings = Keys(("Docs:Ui", "Bogus")),
            Served = _scalarAtApiDocs,
            Warning = "Endatix:Api:Docs:Ui 'Bogus' is not a docs UI. Use one of Scalar, SwaggerUi, Swagger, None.",
        },
        ["Docs Ui None"] = new() { Settings = Keys(("Docs:Ui", "None")), Served = _nothingAtApiDocs },
        ["UseSwagger false"] = new() { Settings = Keys(("UseSwagger", "false")), Served = _nothingAtApiDocs, Document = false },
        ["Docs Enabled true wins over UseSwagger false"] = new()
        {
            Settings = Keys(("Docs:Enabled", "true"), ("UseSwagger", "false")),
            Served = _scalarAtApiDocs,
            Warning = "Endatix:Api:Docs:Enabled 'True' and the deprecated Endatix:Api:UseSwagger 'False'",
        },
        ["Docs Enabled false wins over UseSwagger true"] = new()
        {
            Settings = Keys(("Docs:Enabled", "false"), ("UseSwagger", "true")),
            Served = _nothingAtApiDocs,
            Document = false,
            Warning = "Endatix:Api:Docs:Enabled 'False' and the deprecated Endatix:Api:UseSwagger 'True'",
        },
        ["Docs Path empty"] = new()
        {
            Settings = Keys(("Docs:Path", "")),
            Served = _scalarAtApiDocs,
            Root = Status.Redirect("/api-docs/"),
            Warning = "Endatix:Api:Docs:Path ''",
        },
        ["SwaggerPath /x"] = new()
        {
            Settings = Keys(("SwaggerPath", "/x")),
            Served = _nothingAtApiDocs with { ScalarPage = "/x/" },
        },
        ["SwaggerPath empty"] = new()
        {
            Settings = Keys(("SwaggerPath", "")),
            Served = _scalarAtApiDocs,
            Root = Status.Redirect("/api-docs/"),
            Warning = "Endatix:Api:SwaggerPath ''",
        },
        ["Docs Path wins over SwaggerPath"] = new()
        {
            Settings = Keys(("Docs:Path", "/new"), ("SwaggerPath", "/old")),
            Served = _nothingAtApiDocs with { ScalarPage = "/new/" },
            Warning = "Endatix:Api:Docs:Path '/new' and the deprecated Endatix:Api:SwaggerPath '/old'",
        },
    };

    [Theory]
    [InlineData("no keys")]
    [InlineData("Docs Ui SwaggerUi")]
    [InlineData("Docs Ui swagger alias")]
    [InlineData("Docs Ui Bogus")]
    [InlineData("Docs Ui None")]
    [InlineData("UseSwagger false")]
    [InlineData("Docs Enabled true wins over UseSwagger false")]
    [InlineData("Docs Enabled false wins over UseSwagger true")]
    [InlineData("Docs Path empty")]
    [InlineData("SwaggerPath /x")]
    [InlineData("SwaggerPath empty")]
    [InlineData("Docs Path wins over SwaggerPath")]
    public async Task Configured_host_starts_and_serves_what_its_settings_promise(string rowName)
    {
        // Arrange
        var row = _compatibilityRows[rowName];
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(row.Settings, log: log);
        using var client = host.CreateClient();

        // Assert
        await AssertStatusAsync(client, "/api-docs", row.Served.ApiDocs);
        await AssertStatusAsync(client, "/api-docs/", row.Served.ApiDocsSlash);
        await AssertStatusAsync(client, "/api-docs/index.html", row.Served.ApiDocsIndex);
        await AssertStatusAsync(client, "/swagger", Status.NotFound);
        await AssertStatusAsync(client, SWAGGER_UI_PAGE, Status.NotFound);
        await AssertStatusAsync(client, DOCUMENT_PATH, row.Document ? Status.Ok : Status.NotFound);
        await AssertStatusAsync(client, "/", row.Root);
        await AssertPagesAsync(client, row.Served);
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
        await AssertStatusAsync(client, "/api/api-docs/index.html", Status.Redirect("./"));
    }

    [Fact]
    public async Task Docs_added_first_with_an_explicit_path_log_no_conflict_for_the_path_they_replaced()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(
            Keys(("Docs:Path", "/new"), ("SwaggerPath", "/old")),
            beforeApi: app => app.UseEndatixApiDocs(options => options.Docs.Path = "/given"),
            log: log);
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/given/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await ScalarPage.ReadAsync(client, "/new/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        log.Warnings.Should().NotContain(line => line.Contains("Endatix:Api:Docs:Path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Scalar", "/docs/")]
    [InlineData("SwaggerUi", "/docs/index.html")]
    public async Task Docs_path_without_a_leading_slash_serves_at_that_path(string ui, string page)
    {
        // Arrange
        var settings = Keys(("Docs:Path", "docs"), ("Docs:Ui", ui));

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(settings);
        using var client = host.CreateClient();

        // Assert
        (await GetStatusAsync(client, page)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Explicit_docs_call_in_code_serves_the_docs_when_the_configuration_turns_them_off()
    {
        // Arrange
        var settings = Keys(("UseSwagger", "false"));

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(
            settings, beforeApi: app => app.UseEndatixApiDocs(options => options.Docs.Path = "/x"));
        using var client = host.CreateClient();

        // Assert
        (await ScalarPage.ReadAsync(client, "/x/")).DocumentUrls.Should().Equal(DOCUMENT_URL);
        (await GetStatusAsync(client, "/api-docs/")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Configured_host_logs_one_serving_line_naming_the_page_and_its_ui()
    {
        // Arrange
        var log = new WarningLog();

        // Act
        await using var host = await ApiDocsHost.StartFromConfigAsync(Keys(("Docs:Ui", "SwaggerUI")), log: log);

        // Assert
        log.Information.Should().ContainSingle(line => line.StartsWith("Serving the API docs", StringComparison.Ordinal))
            .Which.Should().Be("Serving the API docs at /api-docs with SwaggerUi");
    }

    private static async Task AssertStatusAsync(HttpClient client, string path, Status expected)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(expected.Code, path);
        response.Headers.Location?.OriginalString.Should().Be(expected.Location, path);
    }

    private static async Task AssertPagesAsync(HttpClient client, Served served)
    {
        if (served.ScalarPage is not null)
        {
            (await ScalarPage.ReadAsync(client, served.ScalarPage)).DocumentUrls.Should().Equal(DOCUMENT_URL);
        }

        if (served.SwaggerUiPage is not null)
        {
            var html = await client.GetStringAsync(new Uri(served.SwaggerUiPage, UriKind.Relative), TestContext.Current.CancellationToken);
            html.Should().Contain("swagger-ui").And.NotContain("scalar.js");
        }
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

    private static Dictionary<string, string?> Keys(params (string Key, string? Value)[] settings) =>
        settings.ToDictionary(setting => $"Endatix:Api:{setting.Key}", setting => setting.Value);

    /// <summary>A status and, for a redirect, the Location header as the server sent it.</summary>
    private sealed record Status(HttpStatusCode Code, string? Location = null)
    {
        public static readonly Status Ok = new(HttpStatusCode.OK);

        public static readonly Status NotFound = new(HttpStatusCode.NotFound);

        public static Status Redirect(string location) => new(HttpStatusCode.Redirect, location);
    }

    /// <summary>What the <c>/api-docs</c> URLs answer, and where Scalar or Swagger UI shows the document.</summary>
    private sealed record Served
    {
        public required Status ApiDocs { get; init; }

        public required Status ApiDocsSlash { get; init; }

        public required Status ApiDocsIndex { get; init; }

        public string? ScalarPage { get; init; }

        public string? SwaggerUiPage { get; init; }
    }

    /// <summary>One configuration and what it serves. <c>/swagger</c> answers not found in every row.</summary>
    private sealed class CompatibilityRow
    {
        public required Dictionary<string, string?> Settings { get; init; }

        public required Served Served { get; init; }

        public bool Document { get; init; } = true;

        public Status Root { get; init; } = Status.NotFound;

        public string? Warning { get; init; }
    }
}
