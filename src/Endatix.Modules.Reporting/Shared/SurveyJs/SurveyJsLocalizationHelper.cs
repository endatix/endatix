using System.Text.Json;
using System.Text.RegularExpressions;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// Helper methods for reading and writing localized strings in SurveyJS format.
/// </summary>
internal static class SurveyJsLocalizationHelper
{
    private const string DefaultLocale = SurveyJsPropertyNames.DefaultLocale;

    private static readonly HashSet<string> _localizedPropertyNames = new(StringComparer.Ordinal)
    {
        SurveyJsPropertyNames.Title,
        SurveyJsPropertyNames.Description,
        SurveyJsPropertyNames.Text,
        SurveyJsPropertyNames.LabelTrue,
        SurveyJsPropertyNames.LabelFalse,
        SurveyJsPropertyNames.OtherText,
    };

    private static readonly Regex _localeKeyPattern = new(
        @"^[a-z]{2,3}(?:-[a-zA-Z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

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
                : new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultLocale] = value };
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
        HashSet<string> locales = new(StringComparer.Ordinal) { DefaultLocale };
        CollectLocales(definition, locales);
        List<string> ordered = [DefaultLocale];
        foreach (var locale in locales.Where(locale => locale != DefaultLocale).OrderBy(locale => locale, StringComparer.Ordinal))
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
                TryCollectLocalesFromProperty(property.Name, property.Value, locales);
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

    private static void TryCollectLocalesFromProperty(string propertyName, JsonElement value, HashSet<string> locales)
    {
        if (!IsLocalizedStringMap(value))
        {
            return;
        }

        if (!IsLocalizedProperty(propertyName) && !HasOnlyLocaleKeys(value))
        {
            return;
        }

        foreach (var localeProperty in value.EnumerateObject())
        {
            if (IsLocaleKey(localeProperty.Name))
            {
                locales.Add(localeProperty.Name);
            }
        }
    }

    private static bool IsLocalizedProperty(string propertyName) =>
        _localizedPropertyNames.Contains(propertyName);

    private static bool IsLocalizedStringMap(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var hasStringChild = false;
        foreach (var child in value.EnumerateObject())
        {
            if (child.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            hasStringChild = true;
        }

        return hasStringChild;
    }

    private static bool HasOnlyLocaleKeys(JsonElement value)
    {
        var hasLocaleKey = false;
        foreach (var property in value.EnumerateObject())
        {
            if (!IsLocaleKey(property.Name))
            {
                return false;
            }

            hasLocaleKey = true;
        }

        return hasLocaleKey;
    }

    private static bool IsLocaleKey(string key) =>
        key == DefaultLocale || _localeKeyPattern.IsMatch(key);

    internal static void WriteLocalizedStrings(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> values)
    {
        writer.WriteStartObject();
        if (values.TryGetValue(DefaultLocale, out var defaultValue))
        {
            writer.WriteString(DefaultLocale, defaultValue);
        }

        foreach (var entry in values
                     .Where(entry => entry.Key != DefaultLocale)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            writer.WriteString(entry.Key, entry.Value);
        }

        writer.WriteEndObject();
    }
}
