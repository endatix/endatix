using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Where a question sits in the form: its page, with the page title in every locale, and its nearest titled panel
/// in the default locale.
/// </summary>
internal sealed record QuestionPlacement(
    string? PageName,
    IReadOnlyDictionary<string, string> PageTitle,
    string? ParentPanelTitle)
{
    /// <summary>A question declared on the survey root, outside any page.</summary>
    public static readonly QuestionPlacement None = new(null, new Dictionary<string, string>(), null);

    public QuestionPlacement InPanel(string? panelTitle) =>
        this with { ParentPanelTitle = panelTitle ?? ParentPanelTitle };

    /// <summary>Writes the placement into a codebook question entry, leaving out what is not known.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.ParentPanelTitle, ParentPanelTitle);
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.PageName, PageName);
        if (PageTitle.Count > 0)
        {
            writer.WritePropertyName(FormSchemaCodebookPropertyNames.PageTitle);
            SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, PageTitle);
        }
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }
}
