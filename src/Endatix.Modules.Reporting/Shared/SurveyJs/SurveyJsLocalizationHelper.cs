using System.Text.Json;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// Helper methods for reading and writing localized strings in SurveyJS format.
/// </summary>
internal static class SurveyJsLocalizationHelper
{
    /// <summary>
    /// Reads localized strings from a JSON element.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="propertyName">The name of the property to read.</param>
    /// <returns>A dictionary of localized strings.</returns>
    internal static IReadOnlyDictionary<string, string> ReadLocalizedStrings(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            return value is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal) { ["default"] = value };
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        Dictionary<string, string> localized = new(StringComparer.Ordinal);
        foreach (var item in property.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.String)
            {
                var value = item.Value.GetString();
                if (value is not null)
                {
                    localized[item.Name] = value;
                }
            }
        }

        return localized;
    }

    /// <summary>
    /// Discovers the locales used in a SurveyJS definition.
    /// </summary>
    /// <param name="definition">The SurveyJS definition to discover locales from.</param>
    /// <returns>A list of locales.</returns>
    internal static List<string> DiscoverLocales(JsonElement definition)
    {
        HashSet<string> locales = new(StringComparer.Ordinal) { "default" };
        CollectLocales(definition, locales);
        List<string> ordered = ["default"];
        foreach (var locale in locales.Where(locale => locale != "default").OrderBy(locale => locale, StringComparer.Ordinal))
        {
            ordered.Add(locale);
        }

        return ordered;
    }

    private static void CollectLocales(JsonElement element, HashSet<string> locales)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.EnumerateObject().Any(child => child.Value.ValueKind == JsonValueKind.String))
                {
                    foreach (var localeProperty in property.Value.EnumerateObject())
                    {
                        if (localeProperty.Value.ValueKind == JsonValueKind.String)
                        {
                            locales.Add(localeProperty.Name);
                        }
                    }
                }

                CollectLocales(property.Value, locales);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectLocales(item, locales);
            }
        }
    }

    internal static void WriteLocalizedStrings(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> values)
    {
        writer.WriteStartObject();
        if (values.TryGetValue("default", out var defaultValue))
        {
            writer.WriteString("default", defaultValue);
        }

        foreach (var entry in values
                     .Where(entry => entry.Key != "default")
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            writer.WriteString(entry.Key, entry.Value);
        }

        writer.WriteEndObject();
    }
}
