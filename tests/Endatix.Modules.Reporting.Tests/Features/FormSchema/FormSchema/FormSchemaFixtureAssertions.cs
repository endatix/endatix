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
    }
}
