using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

/// <summary>The same merged choice lists decide which panel leaves are categorical in the CSV and its Shoji metadata.</summary>
internal sealed class CrunchQuestions
{
    private readonly HashSet<string> _codedQuestions;

    public CrunchQuestions(JsonElement book, SpecialChoiceCodes? settings, MergedFormSchema? map = null)
    {
        Entries = CodebookQuestions.Read(book);
        _codedQuestions = settings is null ? [] : MatchingQuestions(new ChoiceCodes(settings));
        OtherTextKeys = map is null || _codedQuestions.Count == 0 ? [] : CompanionKeys(map);
    }

    public IReadOnlyDictionary<string, JsonElement> Entries { get; }

    public Dictionary<string, string> OtherTextKeys { get; }

    private Dictionary<string, string> CompanionKeys(MergedFormSchema map)
    {
        var used = map.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        foreach (var column in map.Columns.Where(IsCodedPanel))
        {
            if (!Entries[column.SourceQuestion!].TryGetChoices(out var choices) ||
                !choices.EnumerateArray().Any(choice => CatalogChoice.Read(choice)?.Kind == ChoiceKind.Other))
            {
                continue;
            }

            var stem = ExportPathBuilder.ChoiceOtherTextKey(column.Key);
            keys.Add(column.Key, FreeKey(stem, used));
        }
        return keys;
    }

    internal static string FreeKey(string stem, HashSet<string> used)
    {
        var key = stem;
        var n = 2;
        while (!used.Add(key))
        {
            key = $"{stem}_{n++}";
        }
        return key;
    }

    public bool IsCodedPanel(FormSchemaColumn column) =>
        column.Kind is FormSchemaColumnKind.PanelDynamicIndex or FormSchemaColumnKind.NestedLoop &&
        column.SourceQuestion is { } name && _codedQuestions.Contains(name);

    private HashSet<string> MatchingQuestions(ChoiceCodes codes) =>
        Entries.Where(entry => Matches(entry.Value, codes)).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

    private static bool Matches(JsonElement question, ChoiceCodes codes) =>
        FormSchemaCodebookExportShape.OfQuestionEntry(question) == FormSchemaCodebookExportShape.Categorical.Name &&
        question.TryGetChoices(out var choices) &&
        choices.EnumerateArray().Any(choice => CatalogChoice.Read(choice) is { } item && codes.Configures(item.Kind));
}
