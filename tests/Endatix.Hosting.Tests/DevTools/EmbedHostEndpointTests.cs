using Endatix.Hosting.DevTools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Endatix.Hosting.Tests.DevTools;

public sealed class EmbedHostEndpointTests
{
    [Fact]
    public async Task ExecuteAsync_Disabled_Returns404()
    {
        // Arrange
        var context = CreateContext(new EmbedHostOptions { Enabled = false });

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_MissingFormId_ReturnsBuilder()
    {
        // Arrange
        var context = CreateContext(new EmbedHostOptions
        {
            Enabled = true,
            HubBaseUrl = "http://localhost:3000"
        });

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var html = await ReadBody(context);
        html.Should().Contain("name=\"formId\"");
        html.Should().NotContain("/embed/v1/embed.js");
        html.Should().NotContain("Open in new tab");
    }

    [Fact]
    public async Task ExecuteAsync_BareMissingFormId_Returns400()
    {
        // Arrange
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?view=bare");

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ValidFormId_ReturnsHtml()
    {
        // Arrange
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?formId=42");

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var html = await ReadBody(context);
        html.Should().Contain("data-form-id=\"42\"");
        html.Should().Contain("/embed/v1/embed.js");
        html.Should().Contain("id=\"embed-event-log\"");
        html.Should().Contain("view=bare");
        html.Should().Contain("Open in new tab");
        context.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidFormId_ReturnsBuilderWithError()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?formId=abc");

        await EmbedHostEndpoint.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var html = await ReadBody(context);
        html.Should().Contain("formId must be a positive integer.");
        html.Should().Contain("value=\"abc\"");
        html.Should().NotContain("/embed/v1/embed.js");
    }

    [Fact]
    public async Task ExecuteAsync_HttpsPageHttpHub_DoesNotInjectScript()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000", AllowLoopback = true },
            path: "/dev/embed-host?formId=42",
            isHttps: true);

        await EmbedHostEndpoint.ExecuteAsync(context);

        var html = await ReadBody(context);
        html.Should().Contain("mixed content");
        html.Should().Contain("Open HTTP playground");
        html.Should().Contain("http://localhost:5000/dev/embed-host");
        html.Should().NotContain("src=\"http://localhost:3000/embed/v1/embed.js\"");
    }

    [Fact]
    public async Task ExecuteAsync_PathBase_PrefixesBareLink()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?formId=42",
            pathBase: "/api");

        await EmbedHostEndpoint.ExecuteAsync(context);

        var html = await ReadBody(context);
        html.Should().Contain("href=\"/api/dev/embed-host?formId=42&amp;view=bare\"");
        html.Should().NotContain("action=\"/dev/embed-host\"");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedHub_ReturnsBuilderWithError()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000", AllowLoopback = false },
            path: "/dev/embed-host?formId=42&hubBaseUrl=https://evil.example");

        await EmbedHostEndpoint.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var html = await ReadBody(context);
        html.Should().Contain("hubBaseUrl is missing or not allowlisted.");
        html.Should().Contain("value=\"https://evil.example\"");
        html.Should().NotContain("src=\"https://evil.example");
    }

    [Fact]
    public async Task ExecuteAsync_BareHttpsHttpHub_ReturnsBuilderMixedContentCard()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000", AllowLoopback = true },
            path: "/dev/embed-host?formId=42&view=bare",
            isHttps: true);

        await EmbedHostEndpoint.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var html = await ReadBody(context);
        html.Should().Contain("empty--error");
        html.Should().Contain("Open HTTP playground");
        html.Should().Contain("id=\"toggle-config\"");
        html.Should().Contain("http://localhost:5000/dev/embed-host");
        html.Should().NotContain("/embed/v1/embed.js");
    }

    [Fact]
    public async Task ExecuteAsync_HeightModeFillIsCaseInsensitive()
    {
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?formId=42&heightMode=FILL");

        await EmbedHostEndpoint.ExecuteAsync(context);

        var html = await ReadBody(context);
        html.Should().Contain("data-height-mode=\"fill\"");
        html.Should().Contain("class=\"frame frame--fill\"");
    }

    [Fact]
    public async Task ExecuteAsync_BareView_OmitsBuilderChrome()
    {
        // Arrange
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000" },
            path: "/dev/embed-host?formId=42&view=bare&heightMode=fill");

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        var html = await ReadBody(context);
        html.Should().Contain("data-form-id=\"42\"");
        html.Should().Contain("data-height-mode=\"fill\"");
        html.Should().Contain("id=\"endatix-embed-root\"");
        html.Should().NotContain("id=\"embed-event-log\"");
        html.Should().NotContain("Open in new tab");
        html.Should().NotContain("name=\"formId\"");
    }

    [Fact]
    public async Task ExecuteAsync_HttpsPageNoHttpBinding_OffersNoDeadLink()
    {
        // Arrange
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000", AllowLoopback = true },
            path: "/dev/embed-host?formId=42",
            isHttps: true,
            serverAddresses: ["https://localhost:5001"]);

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        var html = await ReadBody(context);
        html.Should().Contain("mixed content");
        html.Should().NotContain("Open HTTP playground");
        html.Should().NotContain("localhost:5000");
    }

    [Fact]
    public async Task ExecuteAsync_HttpsPage_LinksTheServersActualHttpPort()
    {
        // Arrange
        var context = CreateContext(
            new EmbedHostOptions { Enabled = true, HubBaseUrl = "http://localhost:3000", AllowLoopback = true },
            path: "/dev/embed-host?formId=42",
            isHttps: true,
            serverAddresses: ["https://localhost:5001", "http://localhost:57678"]);

        // Act
        await EmbedHostEndpoint.ExecuteAsync(context);

        // Assert
        var html = await ReadBody(context);
        html.Should().Contain("http://localhost:57678/dev/embed-host?formId=42");
        html.Should().NotContain("localhost:5000");
    }

    private static async Task<string> ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext CreateContext(
        EmbedHostOptions options,
        string path = "/dev/embed-host",
        string pathBase = "",
        bool isHttps = false,
        string[]? serverAddresses = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(MicrosoftOptions.Create(options));
        services.AddSingleton<IServer>(new FakeServer(serverAddresses
            ?? ["https://localhost:5001", "http://localhost:5000"]));
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        http.Request.Scheme = isHttps ? "https" : "http";
        http.Request.Host = new HostString(isHttps ? "localhost:5001" : "localhost");
        http.Request.PathBase = pathBase;
        http.Request.Path = path.Split('?')[0];
        if (path.Contains('?', StringComparison.Ordinal))
        {
            http.Request.QueryString = new QueryString(path[path.IndexOf('?', StringComparison.Ordinal)..]);
        }

        http.Response.Body = new MemoryStream();
        return http;
    }

    private sealed class FakeServer(string[] addresses) : IServer
    {
        public IFeatureCollection Features { get; } = Build(addresses);

        public void Dispose() { }

        public Task StartAsync<TContext>(
            IHttpApplication<TContext> application,
            CancellationToken cancellationToken) where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static IFeatureCollection Build(string[] addresses)
        {
            var features = new FeatureCollection();
            var addressFeature = new ServerAddressesFeature();
            foreach (var address in addresses)
            {
                addressFeature.Addresses.Add(address);
            }

            features.Set<IServerAddressesFeature>(addressFeature);
            return features;
        }
    }
}
