using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Describes the submissions-grid columns of a compiled form schema, in schema order.
/// </summary>
internal static class SubmissionGridColumnCatalog
{
    internal static IReadOnlyList<SubmissionGridColumn> Build(FormSchemaEntity schema, string locale, FormSearchKeyStatus search)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        CodebookInLocale codebook = new(
            FormSchemaCodebookReader.ReadQuestions(schema.Codebook),
            FormSchemaCodebookReader.ReadColumns(schema.Codebook),
            locale,
            search);

        return flatteningMap.Columns.Select(codebook.Describe).ToList();
    }

    private sealed record CodebookInLocale(
        IReadOnlyDictionary<string, JsonElement> Questions,
        IReadOnlyDictionary<string, JsonElement> Columns,
        string Locale,
        FormSearchKeyStatus Search)
    {
        public SubmissionGridColumn Describe(FormSchemaColumn column)
        {
            JsonElement? question = Questions.TryGetValue(column.SourceQuestion ?? column.Key, out var found) ? found : null;
            var searchKey = Search.Keys.Find(column.Key);

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
                PanelTitle: question?.GetStringProperty(FormSchemaCodebookPropertyNames.ParentPanelTitle),
                Searchable: searchKey is not null,
                SearchPending: searchKey is not null && Search.IsPending(searchKey),
                SearchSource: searchKey?.Source);
        }
    }
}
