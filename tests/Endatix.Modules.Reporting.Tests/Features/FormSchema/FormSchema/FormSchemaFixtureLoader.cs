using System.Text.Json;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

internal static class FormSchemaFixtureLoader
{
    internal static string SourceFixturesRoot =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Features", "FormSchema", "FlattenedFormDefinition", "Fixtures"));

    internal static string SourceAllQuestionsFixturesRoot =>
        Path.Combine(SourceFixturesRoot, "AllQuestions");

    private static string FixturesRoot =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Features",
            "FormSchema",
            "FlattenedFormDefinition",
            "Fixtures");

    internal static JsonElement LoadDefinition(string fixtureName) => LoadJson(fixtureName);

    internal static JsonElement LoadCustomerExcerptDefinition(string fixtureName) =>
        LoadJson(Path.Combine("CustomerExcerpts", fixtureName));

    internal static JsonElement LoadAllQuestions(string fixtureName) =>
        LoadJson(Path.Combine("AllQuestions", fixtureName));

    internal static string LoadText(string fixtureName) =>
        File.ReadAllText(Path.Combine(FixturesRoot, fixtureName));

    internal static string LoadAllQuestionsText(string fixtureName) =>
        LoadText(Path.Combine("AllQuestions", fixtureName));

    internal static IReadOnlyList<string> LoadCustomerExcerptExpectedKeys(string fixtureName) =>
        LoadExpectedKeys(Path.Combine("CustomerExcerpts", fixtureName));

    internal static IReadOnlyList<string> LoadAllQuestionsExpectedKeys() =>
        LoadExpectedKeys(Path.Combine("AllQuestions", "all-questions-expected-keys.json"));

    internal static JsonElement LoadAllQuestionsExpectedCodebook() =>
        LoadAllQuestions("all-questions-expected-codebook.json");

    internal static JsonElement LoadJson(string fixtureName)
    {
        string path = Path.Combine(FixturesRoot, fixtureName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    internal static IReadOnlyList<string> LoadExpectedKeys(string fixtureName)
    {
        JsonElement array = LoadJson(fixtureName);
        return array.EnumerateArray().Select(item => item.GetString()!).ToList();
    }
}
