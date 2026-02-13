using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Infrastructure.Exporting;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class JsonNodeParserTests
{
    [Fact]
    public void ToJsonNode_ReturnsNull_WhenValueIsNull()
    {
        var node = JsonNodeParser.ToJsonNode(null);

        Assert.Null(node);
    }

    [Fact]
    public void ToJsonNode_ReturnsSameNode_WhenValueIsJsonNode()
    {
        JsonNode original = new JsonObject { ["a"] = 1 };

        var node = JsonNodeParser.ToJsonNode(original);

        Assert.Same(original, node);
    }

    [Fact]
    public void ToJsonNode_ParsesJsonElementObject_ToJsonObject()
    {
        using var doc = JsonDocument.Parse("""{"content":"https://example.com/file.jpg","name":"f.jpg"}""");

        var node = JsonNodeParser.ToJsonNode(doc.RootElement);

        var obj = Assert.IsType<JsonObject>(node);
        Assert.Equal("https://example.com/file.jpg", obj["content"]!.GetValue<string>());
        Assert.Equal("f.jpg", obj["name"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_ParsesJsonElementArray_ToJsonArray()
    {
        using var doc = JsonDocument.Parse("""[{"content":"u1"},{"content":"u2"}]""");

        var node = JsonNodeParser.ToJsonNode(doc.RootElement);

        var array = Assert.IsType<JsonArray>(node);
        Assert.Equal("u1", array[0]!["content"]!.GetValue<string>());
        Assert.Equal("u2", array[1]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_WrapsJsonElementString_AsJsonValue()
    {
        using var doc = JsonDocument.Parse("\"hello\"");

        var node = JsonNodeParser.ToJsonNode(doc.RootElement);

        var value = Assert.IsAssignableFrom<JsonValue>(node);
        Assert.True(value.TryGetValue<string>(out var s));
        Assert.Equal("hello", s);
    }

    [Fact]
    public void ToJsonNode_WrapsJsonElementNumber_AsJsonValue()
    {
        using var doc = JsonDocument.Parse("123");

        var node = JsonNodeParser.ToJsonNode(doc.RootElement);

        var value = Assert.IsAssignableFrom<JsonValue>(node);
        Assert.True(value.TryGetValue<int>(out var n));
        Assert.Equal(123, n);
    }

    [Fact]
    public void ToJsonNode_ReturnsNull_WhenStringIsWhitespace()
    {
        var node = JsonNodeParser.ToJsonNode("   ");

        Assert.Null(node);
    }

    [Fact]
    public void ToJsonNode_WrapsNonJsonString_AsJsonValueString()
    {
        var node = JsonNodeParser.ToJsonNode("plain-text");

        var value = Assert.IsAssignableFrom<JsonValue>(node);
        Assert.True(value.TryGetValue<string>(out var s));
        Assert.Equal("plain-text", s);
    }

    [Fact]
    public void ToJsonNode_ParsesJsonStringObject_ToJsonObject()
    {
        var node = JsonNodeParser.ToJsonNode("""{"content":"u1"}""");

        var obj = Assert.IsType<JsonObject>(node);
        Assert.Equal("u1", obj["content"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_ParsesJsonStringArray_ToJsonArray()
    {
        var node = JsonNodeParser.ToJsonNode("""[{"content":"u1"}]""");

        var array = Assert.IsType<JsonArray>(node);
        Assert.Equal("u1", array[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_DoesNotParseWhenHeuristicDoesNotMatch_ReturnsJsonValueString()
    {
        // Starts like JSON but doesn't end like JSON => heuristic should treat as plain string.
        const string input = "{\"content\":\"u1";
        var node = JsonNodeParser.ToJsonNode(input);

        var value = Assert.IsAssignableFrom<JsonValue>(node);
        Assert.True(value.TryGetValue<string>(out var s));
        Assert.Equal(input, s);
    }

    [Fact]
    public void ToJsonNode_ThrowsJsonException_WhenStringLooksLikeJsonButIsInvalid()
    {
        var ex = Assert.Throws<JsonException>(() => JsonNodeParser.ToJsonNode("{ invalid }"));

        Assert.Contains("Invalid JSON string", ex.Message);
    }

    [Fact]
    public void ToJsonNode_SerializesPoco_ToJsonNode()
    {
        var poco = new { a = 1, b = "x" };

        var node = JsonNodeParser.ToJsonNode(poco);

        var obj = Assert.IsType<JsonObject>(node);
        Assert.Equal(1, obj["a"]!.GetValue<int>());
        Assert.Equal("x", obj["b"]!.GetValue<string>());
    }
}

