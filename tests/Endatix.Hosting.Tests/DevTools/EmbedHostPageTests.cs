using System.Text.RegularExpressions;
using Endatix.Hosting.DevTools;

namespace Endatix.Hosting.Tests.DevTools;

public sealed class EmbedHostPageTests
{
    [Fact]
    public void TryParseFormId_PositiveInteger_Normalizes()
    {
        // Arrange & Act
        var parsed = EmbedHostPage.TryParseFormId(" 42 ", out var normalized);

        // Assert
        parsed.Should().BeTrue();
        normalized.Should().Be("42");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void TryParseFormId_Invalid_ReturnsFalse(string? formId)
    {
        // Act
        var parsed = EmbedHostPage.TryParseFormId(formId, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    [Fact]
    public void TryResolveHubBaseUrl_DefaultLocalhost_Succeeds()
    {
        // Arrange
        var options = new EmbedHostOptions { HubBaseUrl = "http://localhost:3000" };

        // Act
        var resolved = EmbedHostPage.TryResolveHubBaseUrl(null, options, out var uri);

        // Assert
        resolved.Should().BeTrue();
        uri.Should().Be(new Uri("http://localhost:3000"));
    }

    [Fact]
    public void TryResolveHubBaseUrl_DisallowedHost_Fails()
    {
        // Arrange
        var options = new EmbedHostOptions { HubBaseUrl = "http://localhost:3000" };

        // Act
        var resolved = EmbedHostPage.TryResolveHubBaseUrl("https://evil.example", options, out _);

        // Assert
        resolved.Should().BeFalse();
    }

    [Fact]
    public void TryResolveHubBaseUrl_AllowlistedHost_Succeeds()
    {
        // Arrange
        var options = new EmbedHostOptions
        {
            HubBaseUrl = "http://localhost:3000",
            AllowedHubHosts = ["hub.example"]
        };

        // Act
        var resolved = EmbedHostPage.TryResolveHubBaseUrl(
            "https://hub.example:443",
            options,
            out var uri);

        // Assert
        resolved.Should().BeTrue();
        uri.Host.Should().Be("hub.example");
    }

    [Fact]
    public void ToRelativeUrl_BareIncludesViewAndFormId()
    {
        // Act
        var url = EmbedHostPage.ToRelativeUrl("42", "fill", null, "tok", null, bare: true);

        // Assert
        url.Should().Be("/dev/embed-host?formId=42&heightMode=fill&token=tok&view=bare");
    }

    [Fact]
    public void RenderBuilderHtml_EncodesAttributesAndIncludesScript()
    {
        // Arrange
        var hub = new Uri("http://localhost:3000");

        // Act
        var html = EmbedHostPage.RenderBuilderHtml("42", hub, "fill", "campaign=spring", null, null);

        // Assert
        html.Should().Contain("data-form-id=\"42\"");
        html.Should().Contain("src=\"http://localhost:3000/embed/v1/embed.js\"");
        html.Should().Contain("data-height-mode=\"fill\"");
        html.Should().Contain("data-prefill=\"campaign=spring\"");
        html.Should().Contain("<script src=");
        html.Should().Contain("href=\"/dev/embed-host?formId=42&amp;heightMode=fill&amp;prefill=campaign%3Dspring&amp;view=bare\"");
    }

    [Fact]
    public void RenderBuilderHtml_EncodesAngleBracketsInPrefill()
    {
        // Act
        var html = EmbedHostPage.RenderBuilderHtml(
            "42",
            new Uri("http://localhost:3000"),
            null,
            "x=\"><script>",
            null,
            null);

        // Assert
        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("data-prefill=\"x=\"><script>\"");
    }

    [Fact]
    public void RenderBuilderHtml_HasCollapsibleConfigAndLog()
    {
        // Act
        var html = EmbedHostPage.RenderBuilderHtml("42", new Uri("http://localhost:3000"), null, null, null, null);

        // Assert
        html.Should().Contain("id=\"toggle-config\"");
        html.Should().Contain("id=\"config\" hidden");

        // One toggle in the topbar, one in the drawer bar: the drawer stays reachable once dismissed.
        Regex.Matches(html, "data-log-toggle aria-expanded").Should().HaveCount(2);
        Regex.Matches(html, "data-log-count>").Should().HaveCount(2);
    }

    [Fact]
    public void RenderBuilderHtml_WithoutFormId_ForcesConfigOpenAndHidesPreviewTools()
    {
        // Act
        var html = EmbedHostPage.RenderBuilderHtml(null, new Uri("http://localhost:3000"), null, null, null, null);

        // Assert
        html.Should().Contain("data-force-open=\"true\"");
        html.Should().NotContain("id=\"copy-snippet\"");
        html.Should().NotContain("data-width=");

        // Preview tools go away with no form, but the log toggle must not.
        Regex.Matches(html, "data-log-toggle aria-expanded").Should().HaveCount(2);
    }

    [Fact]
    public void RenderBuilderHtml_CopySnippetCarriesEncodedScriptTag()
    {
        // Act
        var html = EmbedHostPage.RenderBuilderHtml("42", new Uri("http://localhost:3000"), "fill", null, null, null);

        // Assert
        html.Should().Contain("data-snippet=\"&lt;script src=");
        html.Should().Contain("data-form-id=&quot;42&quot;");
    }

    [Fact]
    public void RenderBuilderHtml_FillModeMarksStageFrame()
    {
        // Act
        var fill = EmbedHostPage.RenderBuilderHtml("42", new Uri("http://localhost:3000"), "fill", null, null, null);
        var auto = EmbedHostPage.RenderBuilderHtml("42", new Uri("http://localhost:3000"), null, null, null, null);

        // Assert
        fill.Should().Contain("class=\"frame frame--fill\"");
        auto.Should().Contain("class=\"frame\"");
    }

    [Fact]
    public void RenderBareHtml_HasNoLog()
    {
        // Act
        var html = EmbedHostPage.RenderBareHtml("42", new Uri("http://localhost:3000"), null, null, null);

        // Assert
        html.Should().Contain("data-form-id=\"42\"");
        html.Should().NotContain("embed-event-log");
    }
}
