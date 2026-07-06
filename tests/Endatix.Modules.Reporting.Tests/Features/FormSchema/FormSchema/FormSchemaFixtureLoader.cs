using System.Text.Json;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

internal static class FormSchemaFixtureLoader
{
    internal static JsonElement LoadDefinition(string fixtureName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Features",
            "FormSchema",
            "FlattenedFormDefinition",
            "Fixtures",
            fixtureName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static JsonElement LoadJson(string fixtureName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Features",
            "FormSchema",
            "FlattenedFormDefinition",
            "Fixtures",
            fixtureName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static IReadOnlyList<string> LoadExpectedKeys(string fixtureName)
    {
        JsonElement array = LoadJson(fixtureName);
        return array.EnumerateArray().Select(item => item.GetString()!).ToList();
    }
}
