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

    public CrunchQuestions(JsonElement book, SpecialChoiceCodes? settings)
    {
        Entries = CodebookQuestions.Read(book);
        _codedQuestions = settings is null ? [] : MatchingQuestions(new ChoiceCodes(settings));
    }

    public IReadOnlyDictionary<string, JsonElement> Entries { get; }

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
