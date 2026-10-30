using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>The question elements of a definition, and the panels each one sits in.</summary>
internal static partial class FormSchemaCodebookBuilder
{
    /// <summary>A panel or dynamic panel a question sits in: its name and its title, or its name when untitled.</summary>
    private sealed record ParentPanel(string Name, string Title);

    /// <summary>A question element and the panels it sits in, outermost first.</summary>
    private sealed record CollectedQuestion(JsonElement Element, IReadOnlyList<ParentPanel> ParentPanels);

    private static Dictionary<string, CollectedQuestion> CollectQuestionElements(JsonElement definition)
    {
        Dictionary<string, CollectedQuestion> elements = new(StringComparer.Ordinal);

        foreach (var page in EnumeratePages(definition))
        {
            if (!page.TryGetElements(out var pageElements))
            {
                continue;
            }

            CollectElements(pageElements, parentPanels: [], elements);
        }

        if (definition.TryGetElements(out var rootElements))
        {
            CollectElements(rootElements, parentPanels: [], elements);
        }

        return elements;
    }

    private static void CollectElements(
        JsonElement elementList,
        IReadOnlyList<ParentPanel> parentPanels,
        Dictionary<string, CollectedQuestion> elements)
    {
        if (elementList.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in elementList.EnumerateArray())
        {
            var type = element.GetSurveyJsType();
            if (SurveyJsElementType.IsNonData(type))
            {
                continue;
            }

            var name = element.GetSurveyJsName();
            if (!string.IsNullOrWhiteSpace(name))
            {
                elements.TryAdd(name, new CollectedQuestion(element, parentPanels));
            }

            if (SurveyJsElementType.Panel.Matches(type) && element.TryGetElements(out var panelChildren))
            {
                CollectElements(panelChildren, WithPanel(parentPanels, element, name), elements);
            }

            if (SurveyJsElementType.PanelDynamic.Matches(type) &&
                element.TryGetTemplateElements(out var templateElements))
            {
                CollectElements(templateElements, WithPanel(parentPanels, element, name), elements);
            }
        }
    }

    // A panel with neither a name nor a title adds nothing: its questions stay in the panel around it.
    private static IReadOnlyList<ParentPanel> WithPanel(
        IReadOnlyList<ParentPanel> parentPanels,
        JsonElement panelElement,
        string? name)
    {
        if (ResolvePanelTitle(panelElement, name) is not { } title)
        {
            return parentPanels;
        }

        var key = string.IsNullOrWhiteSpace(name) ? title : name;
        return [.. parentPanels, new ParentPanel(key, title)];
    }

    private static string? ResolvePanelTitle(JsonElement panelElement, string? fallbackName)
    {
        var localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(
            panelElement,
            SurveyJsPropertyNames.Title);

        if (localized.TryGetValue(FormSchemaCodebookPropertyNames.Default, out var defaultTitle) &&
            !string.IsNullOrWhiteSpace(defaultTitle))
        {
            return defaultTitle;
        }

        var localizedTitle = localized.Values
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        if (localizedTitle is not null)
        {
            return localizedTitle;
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? null : fallbackName;
    }

    // parentPanelTitle stays for readers of codebooks compiled before parentPanels existed.
    private static void WriteParentPanels(Utf8JsonWriter writer, IReadOnlyList<ParentPanel> parentPanels)
    {
        if (parentPanels.Count == 0)
        {
            return;
        }

        writer.WriteString(FormSchemaCodebookPropertyNames.ParentPanelTitle, parentPanels[^1].Title);
        writer.WritePropertyName(FormSchemaCodebookPropertyNames.ParentPanels);
        writer.WriteStartArray();
        foreach (var panel in parentPanels)
        {
            writer.WriteStartObject();
            writer.WriteString(FormSchemaCodebookPropertyNames.PanelName, panel.Name);
            writer.WriteString(FormSchemaCodebookPropertyNames.PanelTitle, panel.Title);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
