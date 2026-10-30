using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
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
        IReadOnlyList<AudiencePropertyName> variables)
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
        IReadOnlyList<AudiencePropertyName> variables)
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

    private static int CatalogRank(FormSchemaColumn column, AudiencePropertyName? variable)
    {
        if (column.Key == PersonalizationColumns.IdentifierKey)
        {
            return 0;
        }

        return variable is null ? 2 : 1;
    }

    private static void Retitle(JsonObject? columns, IReadOnlyList<FormSchemaColumn> block)
    {
        foreach (var column in block)
        {
            if (columns?.TryGetPropertyValue(column.Key, out var entry) == true &&
                entry is JsonObject entryObj &&
                entryObj.TryGetPropertyValue(FormSchemaCodebookPropertyNames.SurveyJsType, out var typeNode) &&
                typeNode?.GetValue<string>() == FormSchemaCodebookPropertyNames.PersonalizationSurveyJsType)
            {
                entryObj[SurveyJsPropertyNames.Title] = new JsonObject { [FormSchemaCodebookPropertyNames.Default] = column.Label };
            }
        }
    }

    private static JsonObject Block(IReadOnlyList<FormSchemaColumn> block)
    {
        JsonObject variables = [];
        foreach (var columnKey in block.Where(c => c.Key != PersonalizationColumns.IdentifierKey).Select(c => c.Key))
        {
            variables[columnKey[PersonalizationColumns.VariablePrefix.Length..]] = columnKey;
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
        if (codebook.TryGetPropertyValue(BlockPropertyName) is not { } block ||
            block.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var key in IdentifierKeys(block).Concat(VariableKeys(block)))
        {
            yield return key;
        }
    }

    private static IEnumerable<string> IdentifierKeys(JsonElement block) =>
        block.TryGetPropertyValue(IdentifierPropertyName) is { } identifier &&
        identifier.ValueKind == JsonValueKind.String
            ? [identifier.GetString() ?? string.Empty]
            : [];

    private static IEnumerable<string> VariableKeys(JsonElement block)
    {
        if (block.TryGetPropertyValue(VariablesPropertyName) is not { } variables ||
            variables.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return variables.EnumerateObject()
            .Where(variable => variable.Value.ValueKind == JsonValueKind.String)
            .Select(variable => variable.Value.GetString() ?? string.Empty);
    }

    // Audience entries have a default-locale title only.
    private static string? TitleOf(JsonElement codebook, string key) =>
        codebook.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Columns) is { } columnsElement &&
        columnsElement.ValueKind == JsonValueKind.Object &&
        columnsElement.TryGetPropertyValue(key) is { } entryElement &&
        entryElement.ValueKind == JsonValueKind.Object &&
        entryElement.TryGetPropertyValue(SurveyJsPropertyNames.Title) is { } titleElement &&
        titleElement.ValueKind == JsonValueKind.Object &&
        titleElement.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Default) is { } textElement &&
        textElement.ValueKind == JsonValueKind.String
            ? textElement.GetString()
            : null;
}
