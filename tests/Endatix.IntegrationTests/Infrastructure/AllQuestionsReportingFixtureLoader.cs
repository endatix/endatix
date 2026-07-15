using System.Text.Json;

namespace Endatix.IntegrationTests;

internal static class AllQuestionsReportingFixtureLoader
{
    private static string FixturesRoot =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AllQuestions");

    internal static string LoadDefinitionText() =>
        File.ReadAllText(Path.Combine(FixturesRoot, "all-questions-definition.json"));

    internal static string LoadSubmissionText() =>
        File.ReadAllText(Path.Combine(FixturesRoot, "all-questions-submission.json"));

    internal static JsonElement LoadExpectedFlat()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixturesRoot, "all-questions-expected-flat.json")));
        return document.RootElement.Clone();
    }
}

internal static class ReportingJsonAssertions
{
    internal static void AssertJsonElementMatches(JsonElement actual, JsonElement expected, string because)
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

            case JsonValueKind.String:
                actual.GetString().Should().Be(expected.GetString(), because);
                break;

            default:
                actual.GetRawText().Should().Be(expected.GetRawText(), because);
                break;
        }
    }
}
