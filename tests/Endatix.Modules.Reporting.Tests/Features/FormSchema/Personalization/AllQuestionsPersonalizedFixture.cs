using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// The all-questions survey with personalization on top: the overlay in
/// <c>Fixtures/AllQuestionsPersonalized/all-questions-personalization-overlay.json</c> binds existing questions to the
/// audience variables and adds questions. The variables are not declared: Hub passes the Audience Properties to Survey
/// Creator as variable presets. The composed definition is committed so it can be pasted into Hub; a test fails when it
/// no longer matches the overlay.
/// </summary>
internal static class AllQuestionsPersonalizedFixture
{
    public const string Folder = "AllQuestionsPersonalized";
    public const string DefinitionFile = "all-questions-personalized-definition.json";
    public const string ResponsesFile = "all-questions-personalized-responses.json";
    private const string OverlayFile = "all-questions-personalization-overlay.json";

    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Path(string fileName) => System.IO.Path.Combine(Folder, fileName);

    public static string SourcePath(string fileName) =>
        System.IO.Path.Combine(FormSchemaFixtureLoader.SourceFixturesRoot, Folder, fileName);

    /// <summary>The all-questions definition with the overlay applied.</summary>
    public static JsonObject Compose()
    {
        var definition = JsonNode.Parse(FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json"))!.AsObject();
        var overlay = JsonNode.Parse(FormSchemaFixtureLoader.LoadText(Path(OverlayFile)))!.AsObject();

        Bind(definition, overlay["bindings"]!.AsObject());
        AddElements(definition, overlay["addElements"]!.AsArray());
        return definition;
    }

    private static void Bind(JsonObject definition, JsonObject bindings)
    {
        foreach (var (questionName, properties) in bindings)
        {
            var question = FindElement(definition, questionName)
                ?? throw new InvalidOperationException($"Overlay binds '{questionName}', which all-questions does not have.");
            foreach (var (property, value) in properties!.AsObject())
            {
                question[property] = value?.DeepClone();
            }
        }
    }

    private static void AddElements(JsonObject definition, JsonArray elements)
    {
        var firstPageElements = definition["pages"]![0]!["elements"]!.AsArray();
        foreach (var element in elements)
        {
            firstPageElements.Add(element!.DeepClone());
        }
    }

    private static JsonObject? FindElement(JsonObject definition, string name) =>
        definition["pages"]!.AsArray()
            .SelectMany(page => Descendants(page!["elements"]?.AsArray()))
            .FirstOrDefault(element => element["name"]?.GetValue<string>() == name);

    private static IEnumerable<JsonObject> Descendants(JsonArray? elements)
    {
        foreach (var element in elements?.Select(node => node!.AsObject()) ?? [])
        {
            yield return element;
            var children = element["elements"]?.AsArray() ?? element["templateElements"]?.AsArray();
            foreach (var child in Descendants(children))
            {
                yield return child;
            }
        }
    }
}
