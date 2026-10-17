using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// The columns of a compiled schema that can go into its search copy, in schema order.
/// </summary>
/// <remarks>
/// A column is searchable when the submissions grid has a filter for it and its question is not read-only in the
/// definition: a read-only question is filled by the form, not by the respondent. A question inside a read-only page
/// or panel is read-only too.
/// </remarks>
internal static class SearchableColumns
{
    private const string ReadOnlyProperty = "readOnly";

    public static IReadOnlyList<SearchableColumn> In(JsonElement definition, MergedFormSchema schema, string codebookJson)
    {
        var questions = FormSchemaCodebookReader.ReadQuestions(codebookJson);
        var readOnly = ReadOnlyQuestionNames(definition);
        List<SearchableColumn> searchable = [];
        foreach (var column in schema.Columns)
        {
            var filterType = FilterTypeOf(column, questions);
            if (filterType is not null && !readOnly.Contains(QuestionName(column)))
            {
                searchable.Add(new SearchableColumn(column.Key, filterType));
            }
        }

        return searchable;
    }

    /// <summary>
    /// The column of the compiled schema a person can make searchable, read-only questions included: they are left
    /// out of the automatic keys only because nobody fills them in, not because they cannot be filtered. Null when the
    /// schema has no such column, or the grid has no filter for it.
    /// </summary>
    public static SearchableColumn? ChosenColumn(FormSchemaEntity schema, string key)
    {
        var column = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap).Columns
            .FirstOrDefault(candidate => candidate.Key == key);
        var filterType = column is null ? null : FilterTypeOf(column, FormSchemaCodebookReader.ReadQuestions(schema.Codebook));
        return filterType is null ? null : new SearchableColumn(key, filterType);
    }

    private static string? FilterTypeOf(FormSchemaColumn column, Dictionary<string, JsonElement> questions)
    {
        JsonElement? question = questions.TryGetValue(QuestionName(column), out var found) ? found : null;
        return SubmissionGridFilterTypes.Resolve(column, question);
    }

    private static string QuestionName(FormSchemaColumn column) => column.SourceQuestion ?? column.Key;

    private static HashSet<string> ReadOnlyQuestionNames(JsonElement definition)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        CollectReadOnly(definition, inheritedReadOnly: false, names);
        return names;
    }

    // Template elements of a dynamic panel are left out: their copies are never searchable.
    private static void CollectReadOnly(JsonElement container, bool inheritedReadOnly, HashSet<string> names)
    {
        foreach (var child in Children(container))
        {
            var readOnly = inheritedReadOnly || child.GetBooleanProperty(ReadOnlyProperty);
            if (readOnly)
            {
                AddNames(child, names);
            }

            CollectReadOnly(child, readOnly, names);
        }
    }

    private static IEnumerable<JsonElement> Children(JsonElement container)
    {
        if (container.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var pages = container.TryGetPages(out var pageArray) ? pageArray.EnumerateArray().ToList() : [];
        var elements = container.TryGetElements(out var elementArray) ? elementArray.EnumerateArray().ToList() : [];
        return pages.Concat(elements);
    }

    private static void AddNames(JsonElement element, HashSet<string> names)
    {
        foreach (var name in new[] { element.GetSurveyJsName(), element.GetSurveyJsValueName() })
        {
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }
    }
}
