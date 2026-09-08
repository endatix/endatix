using Endatix.Hosting.DevTools;

namespace Endatix.Hosting.Tests.DevTools;

public sealed class EmbedHostAssetsTests
{
    [Fact]
    public void Assets_AreEmbeddedAndNonEmpty()
    {
        EmbedHostAssets.BuilderHtml.Should().StartWith("<!doctype html>");
        EmbedHostAssets.BareHtml.Should().StartWith("<!doctype html>");
        EmbedHostAssets.Styles.Should().Contain(".frame--fill");
        EmbedHostAssets.Script.Should().Contain("endatix:form-loaded");
    }

    [Fact]
    public void BuilderHtml_PlaceholdersAreAllSubstituted()
    {
        // Act
        var html = EmbedHostPage.RenderBuilderHtml("42", new Uri("http://localhost:3000"), "fill", null, null, null);

        html.Should().NotContain("__");
    }
}
