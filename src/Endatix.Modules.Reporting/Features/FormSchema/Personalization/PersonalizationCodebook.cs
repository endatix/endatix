using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <summary>
/// The audience block of a codebook as an export shows it: each column under its property's current name, in the
/// order set on the Audience tab. Applied when an export runs, so a rename or reorder never touches the stored schema.
/// </summary>
/// <remarks>
/// The block is the codebook's <c>personalization</c> object. The tabular export and Shoji read the block order from
/// it, so all three exports follow one rule. A codebook without the block (as stored) orders the block by key.
/// </remarks>
internal static class PersonalizationCodebook
{
    public const string BlockPropertyName = "personalization";
    public const string BlockTitle = "Personalized";
    private const string IdentifierPropertyName = "identifier";
    private const string VariablesPropertyName = "variables";

    /// <summary>
    /// Returns the codebook with current audience names and the <c>personalization</c> block; unchanged when the
    /// schema has no audience columns or the form has no properties (the Audience module is off).
    /// </summary>
    public static string Overlay(
        string codebookJson,
        MergedFormSchema flatteningMap,
        IReadOnlyList<PersonalizationVariableInfo> variables)
    {
        var block = variables.Count == 0 ? [] : InCatalogOrder(flatteningMap, variables);
        if (block.Count == 0)
        {
            return codebookJson;
        }

        var codebook = JsonNode.Parse(codebookJson)!.AsObject();
        Retitle(codebook[FormSchemaCodebookPropertyNames.Columns]?.AsObject(), block);
        codebook[BlockPropertyName] = Block(block);
        return codebook.ToJsonString();
    }

    /// <summary>
    /// The audience columns in the codebook's block order, each labelled by its codebook title. Columns the block does
    /// not list follow, the identifier first and the rest by key.
    /// </summary>
    public static IReadOnlyList<FormSchemaColumn> InBlockOrder(MergedFormSchema flatteningMap, JsonElement codebook)
    {
        var rank = BlockKeys(codebook)
            .Select((key, index) => (key, index))
            .ToDictionary(entry => entry.key, entry => entry.index, StringComparer.Ordinal);
        return flatteningMap.Columns
            .Where(column => column.Kind is FormSchemaColumnKind.Personalization)
            .OrderBy(column => rank.GetValueOrDefault(column.Key, int.MaxValue))
            .ThenBy(column => column.Key == PersonalizationColumns.IdentifierKey ? 0 : 1)
            .ThenBy(column => column.Key, StringComparer.Ordinal)
            .Select(column => column with { Label = TitleOf(codebook, column.Key) ?? column.Label })
            .ToList();
    }

    /// <summary>The map with its audience columns last, in the codebook's block order.</summary>
    public static MergedFormSchema WithBlockLast(MergedFormSchema flatteningMap, JsonElement codebook) =>
        new(flatteningMap.Columns
            .Where(column => column.Kind is not FormSchemaColumnKind.Personalization)
            .Concat(InBlockOrder(flatteningMap, codebook)));

    // The identifier, then properties by sort order, then variables no live property describes (deleted, or never a
    // property), by key. A known property shows its current name; the rest keep the name frozen in the schema.
    private static List<FormSchemaColumn> InCatalogOrder(
        MergedFormSchema flatteningMap,
        IReadOnlyList<PersonalizationVariableInfo> variables)
    {
        // A catalog returns one entry per variable name; a duplicate keeps the first rather than failing the export.
        var byKey = variables
            .DistinctBy(variable => variable.VariableName, StringComparer.Ordinal)
            .ToDictionary(variable => PersonalizationColumns.KeyOf(variable.VariableName), StringComparer.Ordinal);
        return flatteningMap.Columns
            .Where(column => column.Kind is FormSchemaColumnKind.Personalization)
            .Select(column => (Column: column, Variable: byKey.GetValueOrDefault(column.Key)))
            .OrderBy(entry => CatalogRank(entry.Column, entry.Variable))
            .ThenBy(entry => entry.Variable?.SortOrder ?? 0)
            .ThenBy(entry => entry.Column.Key, StringComparer.Ordinal)
            .Select(entry => entry.Variable is null ? entry.Column : entry.Column with { Label = entry.Variable.Name })
            .ToList();
    }

    private static int CatalogRank(FormSchemaColumn column, PersonalizationVariableInfo? variable) =>
        column.Key == PersonalizationColumns.IdentifierKey ? 0
        : variable is null ? 2
        : 1;

    private static void Retitle(JsonObject? columns, IReadOnlyList<FormSchemaColumn> block)
    {
        foreach (var column in block)
        {
            if (columns?[column.Key] is JsonObject entry)
            {
                entry[SurveyJsPropertyNames.Title] = new JsonObject { [FormSchemaCodebookPropertyNames.Default] = column.Label };
            }
        }
    }

    private static JsonObject Block(IReadOnlyList<FormSchemaColumn> block)
    {
        JsonObject variables = [];
        foreach (var column in block.Where(column => column.Key != PersonalizationColumns.IdentifierKey))
        {
            variables[column.Key[PersonalizationColumns.VariablePrefix.Length..]] = column.Key;
        }

        JsonObject result = new()
        {
            [SurveyJsPropertyNames.Title] = new JsonObject { [FormSchemaCodebookPropertyNames.Default] = BlockTitle },
        };
        if (block.Any(column => column.Key == PersonalizationColumns.IdentifierKey))
        {
            result[IdentifierPropertyName] = PersonalizationColumns.IdentifierKey;
        }

        result[VariablesPropertyName] = variables;
        return result;
    }

    private static IEnumerable<string> BlockKeys(JsonElement codebook)
    {
        if (Child(codebook, BlockPropertyName, JsonValueKind.Object) is not { } block)
        {
            yield break;
        }

        if (Child(block, IdentifierPropertyName, JsonValueKind.String) is { } identifier)
        {
            yield return identifier.GetString()!;
        }

        var variables = Child(block, VariablesPropertyName, JsonValueKind.Object)?.EnumerateObject() ?? default;
        foreach (var variable in variables.Where(variable => variable.Value.ValueKind is JsonValueKind.String))
        {
            yield return variable.Value.GetString()!;
        }
    }

    // Audience entries have a default-locale title only.
    private static string? TitleOf(JsonElement codebook, string key) =>
        Child(codebook, FormSchemaCodebookPropertyNames.Columns, JsonValueKind.Object) is { } columns &&
        Child(columns, key, JsonValueKind.Object) is { } entry &&
        Child(entry, SurveyJsPropertyNames.Title, JsonValueKind.Object) is { } title &&
        Child(title, FormSchemaCodebookPropertyNames.Default, JsonValueKind.String) is { } text
            ? text.GetString()
            : null;

    private static JsonElement? Child(JsonElement element, string name, JsonValueKind kind) =>
        element.ValueKind is JsonValueKind.Object &&
        element.TryGetProperty(name, out var child) &&
        child.ValueKind == kind
            ? child
            : null;
}
