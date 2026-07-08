using System.Text.Json;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class JsonElementReaderTests
{
    [Theory]
    [InlineData("""{"formId":555}""", "formId", 555L)]
    [InlineData("""{"formId":"555"}""", "formId", 555L)]
    public void TryGetInt64_WithValidNumberOrString_ReturnsValue(string json, string propertyName, long expected)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        long? result = JsonElementReader.TryGetInt64(document.RootElement, propertyName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"formId":true}""")]
    [InlineData("""{"formId":"not-a-number"}""")]
    [InlineData("""{"other":1}""")]
    [InlineData("""[]""")]
    [InlineData("""null""")]
    public void TryGetInt64_WithInvalidOrMissingProperty_ReturnsNull(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        long? result = JsonElementReader.TryGetInt64(document.RootElement, "formId");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("""{"changeKind":"answers,metadata"}""", "answers,metadata")]
    [InlineData("""{"changeKind":null}""", null)]
    public void TryGetString_WithStringOrNull_ReturnsValue(string json, string? expected)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        string? result = JsonElementReader.TryGetString(document.RootElement, "changeKind");

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"changeKind":1}""")]
    [InlineData("""{"other":"x"}""")]
    public void TryGetString_WithNonStringOrMissingProperty_ReturnsNull(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        string? result = JsonElementReader.TryGetString(document.RootElement, "changeKind");

        result.Should().BeNull();
    }
}
