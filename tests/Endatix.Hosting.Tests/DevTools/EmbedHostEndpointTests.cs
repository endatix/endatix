using Endatix.Hosting.DevTools;
using Microsoft.AspNetCore.Http;
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

    private static async Task<string> ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext CreateContext(EmbedHostOptions options, string path = "/dev/embed-host")
    {
        var services = new ServiceCollection();
        services.AddSingleton(MicrosoftOptions.Create(options));
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        http.Request.Path = path.Split('?')[0];
        if (path.Contains('?', StringComparison.Ordinal))
        {
            http.Request.QueryString = new QueryString(path[path.IndexOf('?', StringComparison.Ordinal)..]);
        }

        http.Response.Body = new MemoryStream();
        return http;
    }
}
