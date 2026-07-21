using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Parses and validates persisted <c>FormSchema.Locales</c> JSON.
/// </summary>
public static class FormSchemaLocales
{
    private const string DEFAULT_LOCALE_KEY = "default";

    /// <summary>
    /// Parses the <c>FormSchema.Locales</c> JSON into a list of locale keys.
    /// </summary>
    /// <param name="localesJson">The JSON string to parse.</param>
    /// <returns>A list of locale keys.</returns>
    public static IReadOnlyList<string> Parse(string localesJson)
    {
        if (string.IsNullOrWhiteSpace(localesJson))
        {
            return [DEFAULT_LOCALE_KEY];
        }

        try
        {
            using var document = JsonDocument.Parse(localesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [DEFAULT_LOCALE_KEY];
            }

            List<string> locales = [];
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        locales.Add(value);
                    }
                }
            }

            return locales.Count > 0 ? locales : [DEFAULT_LOCALE_KEY];
        }
        catch (JsonException)
        {
            return [DEFAULT_LOCALE_KEY];
        }
    }

    /// <summary>
    /// Checks if a locale is present in the <c>FormSchema.Locales</c> JSON.
    /// </summary>
    /// <param name="localesJson">The JSON string to check.</param>
    /// <param name="locale">The locale to check.</param>
    /// <returns>True if the locale is present, false otherwise.</returns>
    public static bool Contains(string localesJson, string locale) =>
        Parse(localesJson).Contains(locale, StringComparer.Ordinal);
}
