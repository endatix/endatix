using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Where a question sits in the form: its page and its nearest titled panel. Titles are in the default locale.
/// </summary>
internal sealed record QuestionPlacement(string? PageName, string? PageTitle, string? ParentPanelTitle)
{
    /// <summary>A question declared on the survey root, outside any page.</summary>
    public static readonly QuestionPlacement None = new(null, null, null);

    public QuestionPlacement InPanel(string? panelTitle) =>
        this with { ParentPanelTitle = panelTitle ?? ParentPanelTitle };

    /// <summary>Writes the placement into a codebook question entry, leaving out what is not known.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.ParentPanelTitle, ParentPanelTitle);
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.PageName, PageName);
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.PageTitle, PageTitle);
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }
}
