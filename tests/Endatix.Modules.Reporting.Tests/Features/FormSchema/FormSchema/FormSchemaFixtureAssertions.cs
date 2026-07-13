using System.Text.Json;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

internal static class FormSchemaFixtureAssertions
{
    internal static void AssertFlatMatchesExpected(
        Dictionary<string, JsonElement?> actual,
        JsonElement expected,
        string because)
    {
        foreach (JsonProperty property in expected.EnumerateObject())
        {
            actual.Should().ContainKey(property.Name, because: because);
            JsonElement? actualValue = actual[property.Name];

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                actualValue.Should().BeNull(because);
                continue;
            }

            actualValue.Should().NotBeNull(because);
            actualValue!.Value.GetRawText().Should().Be(property.Value.GetRawText(), because);
        }

        actual.Keys.Should().BeEquivalentTo(
            expected.EnumerateObject().Select(property => property.Name),
            because: because);
    }

    internal static void AssertJsonMatchesExpected(
        JsonElement actual,
        JsonElement expected,
        string because)
    {
        AssertJsonElementMatches(actual, expected, because);
    }

    private static void AssertJsonElementMatches(JsonElement actual, JsonElement expected, string because)
    {
        actual.ValueKind.Should().Be(expected.ValueKind, because);

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                List<JsonProperty> expectedProperties = expected.EnumerateObject().ToList();
                List<JsonProperty> actualProperties = actual.EnumerateObject().ToList();
                actualProperties.Count.Should().Be(expectedProperties.Count, because);

                foreach (JsonProperty expectedProperty in expectedProperties)
                {
                    actual.TryGetProperty(expectedProperty.Name, out JsonElement actualProperty)
                        .Should().BeTrue($"{because}: missing property '{expectedProperty.Name}'");
                    AssertJsonElementMatches(actualProperty, expectedProperty.Value, because);
                }

                break;

            case JsonValueKind.Array:
                List<JsonElement> actualItems = actual.EnumerateArray().ToList();
                List<JsonElement> expectedItems = expected.EnumerateArray().ToList();
                actualItems.Count.Should().Be(expectedItems.Count, because);

                for (var index = 0; index < expectedItems.Count; index++)
                {
                    AssertJsonElementMatches(actualItems[index], expectedItems[index], because);
                }

                break;

            case JsonValueKind.Null:
                break;

            default:
                actual.GetRawText().Should().Be(expected.GetRawText(), because);
                break;
        }
    }
}
