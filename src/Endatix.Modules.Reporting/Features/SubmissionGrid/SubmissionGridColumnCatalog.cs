using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Describes the submissions-grid columns of a compiled form schema, in schema order.
/// </summary>
internal static class SubmissionGridColumnCatalog
{
    internal static IReadOnlyList<SubmissionGridColumn> Build(FormSchemaEntity schema, string locale)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        CodebookInLocale codebook = new(
            FormSchemaCodebookReader.ReadQuestions(schema.Codebook),
            FormSchemaCodebookReader.ReadColumns(schema.Codebook),
            locale);

        return flatteningMap.Columns.Select(codebook.Describe).ToList();
    }

    private sealed record CodebookInLocale(
        IReadOnlyDictionary<string, JsonElement> Questions,
        IReadOnlyDictionary<string, JsonElement> Columns,
        string Locale)
    {
        public SubmissionGridColumn Describe(FormSchemaColumn column)
        {
            JsonElement? question = Questions.TryGetValue(column.SourceQuestion ?? column.Key, out var found) ? found : null;

            return new SubmissionGridColumn(
                Key: column.Key,
                Kind: column.Kind.ToString(),
                Title: FormSchemaCodebookReader.ResolveHeaderLabel(Columns, column.Key, Locale) ?? column.Label,
                QuestionType: question?.GetStringProperty(FormSchemaCodebookPropertyNames.SurveyJsType),
                FilterType: SubmissionGridFilterTypes.Resolve(column, question),
                PageName: question?.GetStringProperty(FormSchemaCodebookPropertyNames.PageName),
                PageTitle: question is { } entry
                    ? FormSchemaCodebookReader.ReadLocalizedString(entry, FormSchemaCodebookPropertyNames.PageTitle, Locale)
                    : null,
                PanelTitle: question?.GetStringProperty(FormSchemaCodebookPropertyNames.ParentPanelTitle));
        }
    }
}
