using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Shared.SurveyJs;

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
            var questionName = column.SourceQuestion ?? column.Key;
            JsonElement? question = questions.TryGetValue(questionName, out var found) ? found : null;
            var filterType = SubmissionGridFilterTypes.Resolve(column, question);
            if (filterType is not null && !readOnly.Contains(questionName))
            {
                searchable.Add(new SearchableColumn(column.Key, filterType));
            }
        }

        return searchable;
    }

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
