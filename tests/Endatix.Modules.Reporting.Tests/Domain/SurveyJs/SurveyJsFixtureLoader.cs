using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Domain.SurveyJs;

internal static class SurveyJsFixtureLoader
{
    internal static JsonElement LoadDefinition(string fixtureName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Domain", "SurveyJs", "Fixtures", fixtureName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static JsonElement LoadJson(string fixtureName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Domain", "SurveyJs", "Fixtures", fixtureName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static IReadOnlyList<string> LoadExpectedKeys(string fixtureName)
    {
        JsonElement array = LoadJson(fixtureName);
        return array.EnumerateArray().Select(item => item.GetString()!).ToList();
    }
}
