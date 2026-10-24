using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// The all-questions survey with personalization on top: the overlay in
/// <c>Fixtures/AllQuestionsPersonalized/all-questions-personalization-overlay.json</c> binds existing questions to the
/// audience variables and adds questions. Its <c>variablePresets</c> declares the variables as Hub passes the Audience
/// Properties to Survey Creator, in the shape the Audience variables read returns, with placeholder Test participants;
/// they stay out of the composed definition. The composed definition and the variable presets are committed so they can
/// be loaded into the designer; a test fails when either no longer matches the overlay.
/// </summary>
internal static partial class AllQuestionsPersonalizedFixture
{
    public const string Folder = "AllQuestionsPersonalized";
    public const string DefinitionFile = "all-questions-personalized-definition.json";
    public const string ResponsesFile = "all-questions-personalized-responses.json";
    public const string VariablePresetsFile = "all-questions-personalized-variable-presets.json";
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
        var overlay = Overlay();

        Bind(definition, overlay["bindings"]!.AsObject());
        AddElements(definition, overlay["addElements"]!.AsArray());
        return definition;
    }

    /// <summary>The overlay's <c>variablePresets</c>: the variables it reads, as Survey Creator takes them.</summary>
    public static JsonObject VariablePresets() => Overlay()["variablePresets"]!.AsObject();

    /// <summary>The variables <see cref="VariablePresets"/> declares, ignoring case as SurveyJS does.</summary>
    public static HashSet<string> VariableNames() =>
        VariablePresets()["definition"]!["elements"]!.AsArray()
            .Select(element => element!["name"]!.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every <c>{name}</c> the overlay's bindings and added questions read.</summary>
    public static IEnumerable<string> References()
    {
        var overlay = Overlay();
        return Strings(overlay["bindings"]).Concat(Strings(overlay["addElements"]))
            .SelectMany(text => Reference().Matches(text))
            .Select(match => match.Groups[1].Value)
            .Distinct();
    }

    /// <summary>Every question in <paramref name="definition"/>, nested ones included, and every calculated value.</summary>
    public static IEnumerable<string> ElementNames(JsonObject definition) =>
        definition["pages"]!.AsArray()
            .SelectMany(page => Descendants(page!["elements"]?.AsArray()))
            .Concat(definition["calculatedValues"]?.AsArray().Select(node => node!.AsObject()) ?? [])
            .Select(element => element["name"]!.GetValue<string>());

    private static JsonObject Overlay() =>
        JsonNode.Parse(FormSchemaFixtureLoader.LoadText(Path(OverlayFile)))!.AsObject();

    private static IEnumerable<string> Strings(JsonNode? node) =>
        node switch
        {
            JsonObject properties => properties.SelectMany(property => Strings(property.Value)),
            JsonArray items => items.SelectMany(Strings),
            JsonValue value when value.TryGetValue(out string? text) => [text],
            _ => [],
        };

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Reference();

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
