using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Which answer filter a submissions-grid column supports.
/// </summary>
/// <remarks>
/// Only a question's own value column is filterable: one text value per submission. Choice indicators, matrix
/// cells, loop and panel copies, files, numbers and booleans have no filter yet.
/// </remarks>
public static class SubmissionGridFilterTypes
{
    /// <summary>Case-insensitive "contains" on the stored value.</summary>
    public const string Text = "text";

    /// <summary>From/To on calendar days.</summary>
    public const string Date = "date";

    /// <summary>From/To on calendar days, for values that carry a time.</summary>
    public const string DateTime = "datetime";

    private static readonly HashSet<string> TextInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "email",
        "tel",
        "url",
    };

    private static readonly HashSet<string> DateTimeInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "datetime",
        "datetime-local",
    };

    /// <summary>
    /// The filter type of <paramref name="column"/>, read from its codebook question; <see langword="null"/> when the
    /// column cannot be filtered.
    /// </summary>
    internal static string? Resolve(FormSchemaColumn column, JsonElement? question)
    {
        var isQuestionValue = column.Kind is FormSchemaColumnKind.Simple &&
                              (column.SourceQuestion is null || column.SourceQuestion == column.Key);
        if (!isQuestionValue || question is not { } entry)
        {
            return null;
        }

        return entry.GetStringProperty(FormSchemaCodebookPropertyNames.SurveyJsType)?.ToLowerInvariant() switch
        {
            "comment" or "dropdown" or "radiogroup" => Text,
            "text" => ResolveTextInput(entry.GetStringProperty(SurveyJsPropertyNames.InputType)),
            _ => null,
        };
    }

    private static string? ResolveTextInput(string? inputType)
    {
        if (string.IsNullOrWhiteSpace(inputType) || TextInputTypes.Contains(inputType))
        {
            return Text;
        }

        if (string.Equals(inputType, "date", StringComparison.OrdinalIgnoreCase))
        {
            return Date;
        }

        return DateTimeInputTypes.Contains(inputType) ? DateTime : null;
    }
}
