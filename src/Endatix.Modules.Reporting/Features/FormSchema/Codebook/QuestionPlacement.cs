using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// A panel or dynamic panel a question sits in: the key it is told apart by, and its default-locale title. The key is
/// the panel's name, or its position (<c>page1#2</c>) when it has none, so two unnamed panels with one title stay two.
/// </summary>
internal sealed record ParentPanel(string Key, string Title)
{
    // The one place a panel's key and title are settled, for a compile and for a codebook read back. A blank title
    // shows the key, a blank key is the title, and a panel with neither is no panel.
    public static ParentPanel? Of(string? key, string? title)
    {
        var shownTitle = string.IsNullOrWhiteSpace(title) ? key : title;
        if (string.IsNullOrWhiteSpace(shownTitle))
        {
            return null;
        }

        return new ParentPanel(string.IsNullOrWhiteSpace(key) ? shownTitle : key, shownTitle);
    }

    /// <summary>The panels a codebook question entry lists, outermost first; empty when it lists none.</summary>
    public static List<ParentPanel> ReadAll(JsonElement question)
    {
        if (question.ValueKind != JsonValueKind.Object ||
            !question.TryGetProperty(FormSchemaCodebookPropertyNames.ParentPanels, out var panels) ||
            panels.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. panels.EnumerateArray()
            .Where(panel => panel.ValueKind == JsonValueKind.Object)
            .Select(panel => Of(
                panel.GetStringProperty(FormSchemaCodebookPropertyNames.PanelKey),
                panel.GetStringProperty(FormSchemaCodebookPropertyNames.PanelTitle)))
            .OfType<ParentPanel>()];
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString(FormSchemaCodebookPropertyNames.PanelKey, Key);
        writer.WriteString(FormSchemaCodebookPropertyNames.PanelTitle, Title);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Where a question sits in the form: its page, with the page title in every locale, and the panels around it,
/// outermost first.
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

    /// <summary>
    /// The placement of a panel's children. <paramref name="position"/> is the panel's index among its siblings,
    /// its key when it has no name. A panel with no title (and so no name) adds nothing.
    /// </summary>
    public QuestionPlacement InPanel(string? panelName, string? panelTitle, int position)
    {
        if (string.IsNullOrWhiteSpace(panelTitle))
        {
            return this;
        }

        var key = string.IsNullOrWhiteSpace(panelName) ? UnnamedPanelKey(position) : panelName;
        return ParentPanel.Of(key, panelTitle) is { } panel
            ? this with { ParentPanels = [.. ParentPanels, panel] }
            : this;
    }

    // Unique among the definition's panels: the position is within the nearest panel, or the page, around it.
    private string UnnamedPanelKey(int position)
    {
        var container = ParentPanels.Count > 0 ? ParentPanels[^1].Key : PageName;
        return $"{container}#{position}";
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
            panel.WriteTo(writer);
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
