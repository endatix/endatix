using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Infrastructure.Exporting.Formatters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Exporting.Formatters;

public class DefaultCsvFormatterTests
{
    private readonly DefaultCsvFormatter _formatter = new();

    // We use a dummy context because DefaultCsvFormatter does not rely on the Row or Logger.
    private readonly TransformationContext<object> _context =
        new(new object(), null, NullLogger.Instance);

    [Theory]
    [MemberData(nameof(GetPrimitiveTestCases))]
    public void Format_ShouldReturnExpectedString_ForPrimitives(object? input, string? expected)
    {
        // Act
        var result = _formatter.Format(input, _context);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetJsonNodeTestCases))]
    public void Format_ShouldHandleJsonNodes_Correctly(JsonNode? input, string? expected)
    {
        // Act
        var result = _formatter.Format(input, _context);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetJsonElementTestCases))]
    public void Format_ShouldHandleJsonElements_Correctly(JsonElement input, string? expected)
    {
        // Act
        var result = _formatter.Format(input, _context);

        // Assert
        Assert.Equal(expected, result);
    }

    // --- Data Generators ---

    public static IEnumerable<object?[]> GetPrimitiveTestCases()
    {
        // 1. Null
        yield return new object?[] { null, null };

        // 2. DateTime (yyyy-MM-dd HH:mm:ss)
        var date = new DateTime(2023, 10, 05, 14, 30, 05);
        yield return new object?[] { date, "2023-10-05 14:30:05" };

        // 3. Bool (Lowercased)
        yield return new object?[] { true, "true" };
        yield return new object?[] { false, "false" };

        // 4. String Lists (Joined)
        yield return new object?[] { new List<string> { "A", "B", "C" }, "A, B, C" };
        yield return new object?[] { new[] { "One" }, "One" };
        yield return new object?[] { new List<string>(), "" };

        // 5. Plain Strings (Unchanged)
        yield return new object?[] { "Simple String", "Simple String" };

        // 6. Numbers (ToString)
        yield return new object?[] { 123, "123" };
        yield return new object?[] { 12.5m, "12.5" };
    }

    public static IEnumerable<object?[]> GetJsonNodeTestCases()
    {
        // 1. JsonValue (String) - Should be unquoted
        yield return new object?[] { JsonValue.Create("test"), "test" };

        // 2. JsonValue (Number/Bool)
        yield return new object?[] { JsonValue.Create(42), "42" };
        yield return new object?[] { JsonValue.Create(true), "true" };

        // 3. JsonArray (Simple Strings) - Should be joined
        yield return new object?[] { JsonNode.Parse("[\"red\", \"blue\"]"), "red, blue" };

        // 4. JsonArray (Simple Numbers) - Should be joined
        yield return new object?[] { JsonNode.Parse("[10, 20]"), "10, 20" };

        // 5. JsonArray (Mixed/Complex) - Should fall back to JSON string
        // Note: The formatter logic keeps complex structures as JSON
        yield return new object?[] { JsonNode.Parse("[{\"id\":1}, {\"id\":2}]"), "[{\"id\":1},{\"id\":2}]" };

        // 6. JsonObject - Should be JSON string
        yield return new object?[] { JsonNode.Parse("{\"key\":\"value\"}"), "{\"key\":\"value\"}" };
    }

    public static IEnumerable<object?[]> GetJsonElementTestCases()
    {
        // Helper to get RootElement from string
        static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

        // 1. String Element - Should be unquoted
        yield return new object?[] { Parse("\"Hello World\""), "Hello World" };

        // 2. Number/Bool Elements
        yield return new object?[] { Parse("99.9"), "99.9" };
        yield return new object?[] { Parse("false"), "false" };

        // 3. Array Element (Simple) - Should be joined
        yield return new object?[] { Parse("[\"apple\", \"banana\"]"), "apple, banana" };

        // 4. Array Element (Empty)
        yield return new object?[] { Parse("[]"), "" };

        // 5. Complex Object Element - Should be JSON string
        // Note: GetRawText() preserves whitespace if parsed from string, 
        // but exact equality depends on how the Formatter handles it.
        // Assuming Formatter uses GetRawText() for Objects:
        var jsonObj = "{\"a\":1}";
        yield return new object?[] { Parse(jsonObj), jsonObj };
    }
}