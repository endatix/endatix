using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Shared.SurveyJs;

public class SurveyJsJsonElementExtensionsTests
{
    [Fact]
    public void GetStringProperty_ReturnsStringValue()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "name": "q1" }""");
        JsonElement element = document.RootElement;

        element.GetStringProperty("name").Should().Be("q1");
    }

    [Fact]
    public void GetStringProperty_MissingOrWrongKind_ReturnsNull()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "name": 1 }""");
        JsonElement element = document.RootElement;

        element.GetStringProperty("name").Should().BeNull();
        element.GetStringProperty("missing").Should().BeNull();
    }

    [Fact]
    public void TryGetArrayProperty_ReturnsArray()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "elements": [ { "type": "text" } ] }""");
        JsonElement element = document.RootElement;

        bool found = element.TryGetElements(out JsonElement elements);

        found.Should().BeTrue();
        elements.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void TryGetInt32Property_ReturnsNumber()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "maxPanelCount": 5 }""");
        JsonElement element = document.RootElement;

        bool found = element.TryGetInt32Property("maxPanelCount", out int value);

        found.Should().BeTrue();
        value.Should().Be(5);
    }

    [Fact]
    public void GetBooleanProperty_UsesDefaultWhenMissing()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "showOtherItem": false }""");
        JsonElement element = document.RootElement;

        element.GetBooleanProperty("showOtherItem").Should().BeFalse();
        element.GetBooleanProperty("missing", defaultValue: true).Should().BeTrue();
    }

    [Fact]
    public void GetSurveyJsTitle_FallsBackWhenMissing()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "title": "  " }""");
        JsonElement element = document.RootElement;

        element.GetSurveyJsTitle("fallback").Should().Be("fallback");
    }

    [Fact]
    public void GetNonEmptyStringProperty_IgnoresWhitespace()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "name": "  " }""");
        JsonElement element = document.RootElement;

        element.GetNonEmptyStringProperty("name").Should().BeNull();
        element.GetNonEmptyStringProperty("missing").Should().BeNull();
    }

    [Fact]
    public void GetNonEmptyStringValue_ReturnsScalarString()
    {
        using JsonDocument document = JsonDocument.Parse(""" "file.pdf" """);
        JsonElement element = document.RootElement;

        element.GetNonEmptyStringValue().Should().Be("file.pdf");
    }

    [Fact]
    public void TryGetPropertyValue_ReturnsNestedValue()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "q1": { "row1": "yes" } }""");
        JsonElement element = document.RootElement;

        JsonElement? answer = element.TryGetPropertyValue("q1");
        answer.Should().NotBeNull();
        answer!.Value.TryGetPropertyValue("row1")!.Value.GetString().Should().Be("yes");
    }

    [Fact]
    public void GetScalarStringValue_ReturnsNumberAsRawText()
    {
        using JsonDocument document = JsonDocument.Parse("1");
        JsonElement element = document.RootElement;

        element.GetScalarStringValue().Should().Be("1");
    }

    [Fact]
    public void TryGetCalculatedValues_ReturnsArray()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "calculatedValues": [ { "name": "total" } ] }""");
        JsonElement element = document.RootElement;

        bool found = element.TryGetCalculatedValues(out JsonElement calculatedValues);

        found.Should().BeTrue();
        calculatedValues.GetArrayLength().Should().Be(1);
    }
}
