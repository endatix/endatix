using System.Text.Json;
using System.Text.RegularExpressions;

namespace Endatix.Modules.Reporting.Features.FormSchema;

internal sealed class SurveyReferenceRoots
{
    private readonly Dictionary<string, int> _references = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase)
    {
        "pageno", "pagecount", "questioncount", "correctedanswers", "correctanswers", "correctedanswercount",
        "incorrectedanswers", "incorrectanswers", "incorrectedanswercount", "locale",
        "panel", "row", "parentpanel", "prevpanel", "panelindex", "rowindex", "item", "self"
    };

    internal static Dictionary<string, int> Unknown(JsonElement document, HashSet<string> declared)
    {
        var survey = new SurveyReferenceRoots();
        survey._known.UnionWith(declared);
        survey.Visit(document, "");
        foreach (var name in survey._known)
        {
            survey._references.Remove(name);
        }
        return survey._references;
    }

    private void Visit(JsonElement value, string collection)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                Visit(item, collection);
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                VisitProperty(property, collection);
            }
        }
    }

    private void VisitProperty(JsonProperty property, string collection)
    {
        if (IsDeclaration(property.Name, collection) && property.Value.ValueKind == JsonValueKind.String)
        {
            _known.Add(property.Value.GetString()!);
        }
        else if (IsExpression(property.Name) || IsPipedText(property.Name))
        {
            ReadText(property.Value, IsExpression(property.Name));
        }
        else if (collection == "triggers" && property.Name == "fromName" && property.Value.ValueKind == JsonValueKind.String)
        {
            Count(property.Value.GetString()!);
        }
        else
        {
            Visit(property.Value, property.Name);
        }
    }

    private static bool IsDeclaration(string name, string collection) =>
        (name is "name" or "valueName" && collection is "pages" or "elements" or "templateElements" or "calculatedValues")
        || (name == "setToName" && collection == "triggers");

    private static bool IsExpression(string name) =>
        name.EndsWith("If", StringComparison.Ordinal) || name.EndsWith("Expression", StringComparison.Ordinal)
        || name is "expression" or "runExpression";

    private static bool IsPipedText(string name) =>
        name is "title" or "description" or "text" or "html" or "defaultValue" or "placeholder" or "url"
        || name.EndsWith("Text", StringComparison.Ordinal) || name.EndsWith("Html", StringComparison.Ordinal);

    private void ReadText(JsonElement value, bool expression)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var localized in value.EnumerateObject())
            {
                ReadText(localized.Value, expression);
            }
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            var matches = (expression ? _expressionTokens : _pipedTokens).Matches(text);
            foreach (var root in matches.Select(match => match.Groups["root"]).Where(group => group.Success))
            {
                Count(root.Value);
            }
        }
    }

    private void Count(string name)
    {
        var root = name.Trim().ToLowerInvariant();
        _references[root] = _references.GetValueOrDefault(root) + 1;
    }

    private const string REFERENCE = @"\{(?<root>[\p{L}\p{N}_$][^{}\[\].\r\n]*)(?:[.\[][^{}]*)?\}";

    private static readonly Regex _pipedTokens = new(REFERENCE, RegexOptions.NonBacktracking);
    private static readonly Regex _expressionTokens = new("""'(?:''|[^'])*'|"(?:""|[^"])*"|""" + REFERENCE, RegexOptions.NonBacktracking);
}
