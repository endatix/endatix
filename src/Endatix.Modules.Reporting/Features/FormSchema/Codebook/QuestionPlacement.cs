using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>A panel or dynamic panel a question sits in: its name, and its title or its name when untitled.</summary>
internal sealed record ParentPanel(string Name, string Title);

/// <summary>
/// Where a question sits in the form: its page, with the page title in every locale, and the panels around it,
/// outermost first, titled in the default locale.
/// </summary>
internal sealed record QuestionPlacement(
    string? PageName,
    IReadOnlyDictionary<string, string> PageTitle,
    IReadOnlyList<ParentPanel> ParentPanels)
{
    /// <summary>A question declared on the survey root, outside any page.</summary>
    public static readonly QuestionPlacement None = new(null, new Dictionary<string, string>(), []);

    /// <summary>The nearest panel's title, what codebooks before <see cref="ParentPanels"/> stored.</summary>
    public string? ParentPanelTitle => ParentPanels.Count > 0 ? ParentPanels[^1].Title : null;

    // A panel with neither a name nor a title adds nothing: its questions stay in the panel around it.
    public QuestionPlacement InPanel(string? panelName, string? panelTitle)
    {
        if (string.IsNullOrWhiteSpace(panelTitle))
        {
            return this;
        }

        var name = string.IsNullOrWhiteSpace(panelName) ? panelTitle : panelName;
        return this with { ParentPanels = [.. ParentPanels, new ParentPanel(name, panelTitle)] };
    }

    /// <summary>Writes the placement into a codebook question entry, leaving out what is not known.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.ParentPanelTitle, ParentPanelTitle);
        WriteParentPanels(writer);
        WriteIfPresent(writer, FormSchemaCodebookPropertyNames.PageName, PageName);
        if (PageTitle.Count > 0)
        {
            writer.WritePropertyName(FormSchemaCodebookPropertyNames.PageTitle);
            SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, PageTitle);
        }
    }

    private void WriteParentPanels(Utf8JsonWriter writer)
    {
        if (ParentPanels.Count == 0)
        {
            return;
        }

        writer.WritePropertyName(FormSchemaCodebookPropertyNames.ParentPanels);
        writer.WriteStartArray();
        foreach (var panel in ParentPanels)
        {
            writer.WriteStartObject();
            writer.WriteString(FormSchemaCodebookPropertyNames.PanelName, panel.Name);
            writer.WriteString(FormSchemaCodebookPropertyNames.PanelTitle, panel.Title);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }
}
