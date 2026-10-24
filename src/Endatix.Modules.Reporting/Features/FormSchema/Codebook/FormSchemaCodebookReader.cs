using System.Globalization;
using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Reads persisted codebook JSON: its question and column entries, and labels in a locale.
/// </summary>
internal static class FormSchemaCodebookReader
{
    /// <summary>Question entries keyed by question name; empty when the codebook has none.</summary>
    internal static Dictionary<string, JsonElement> ReadQuestions(string codebookJson) =>
        ReadSection(codebookJson, FormSchemaCodebookPropertyNames.Questions);

    /// <summary>Column entries keyed by column key; empty when the codebook has none.</summary>
    internal static Dictionary<string, JsonElement> ReadColumns(string codebookJson) =>
        ReadSection(codebookJson, FormSchemaCodebookPropertyNames.Columns);

    /// <summary>
    /// The column's header in <paramref name="locale"/>: the question title, plus the choice, row or column label
    /// when the column is one part of the question. <see langword="null"/> when the codebook does not describe it.
    /// </summary>
    internal static string? ResolveHeaderLabel(
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string canonicalKey,
        string locale)
    {
        if (!codebookColumns.TryGetValue(canonicalKey, out var columnMetadata))
        {
            return null;
        }

        var title = ReadLocalizedString(columnMetadata, "title", locale);
        return WithPart(title, ReadLocalizedString(columnMetadata, "choiceLabel", locale), "{0} ({1})") ??
               WithPart(title, ReadLocalizedString(columnMetadata, "rowLabel", locale), "{0} — {1}") ??
               WithPart(title, ReadLocalizedString(columnMetadata, "columnLabel", locale), "{0} — {1}") ??
               title;
    }

    private static string? WithPart(string? title, string? part, string format)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(title) ? part : string.Format(CultureInfo.InvariantCulture, format, title, part);
    }

    /// <summary>
    /// A plain string property, or a localized one read in <paramref name="locale"/> with the default locale as
    /// fallback.
    /// </summary>
    internal static string? ReadLocalizedString(JsonElement element, string propertyName, string locale)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Object => ReadLocale(property, locale) ?? ReadLocale(property, FormSchemaCodebookPropertyNames.Default),
            _ => null,
        };
    }

    private static string? ReadLocale(JsonElement localized, string locale) =>
        localized.TryGetProperty(locale, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Dictionary<string, JsonElement> ReadSection(string codebookJson, string sectionName)
    {
        Dictionary<string, JsonElement> entries = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(codebookJson))
        {
            return entries;
        }

        using var document = JsonDocument.Parse(codebookJson);
        if (document.RootElement.TryGetProperty(sectionName, out var section) && section.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in section.EnumerateObject())
            {
                entries[property.Name] = property.Value.Clone();
            }
        }

        return entries;
    }
}
