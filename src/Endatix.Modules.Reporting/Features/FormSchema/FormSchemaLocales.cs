using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Parses and validates persisted <c>FormSchema.Locales</c> JSON.
/// </summary>
public static class FormSchemaLocales
{
    public static IReadOnlyList<string> Parse(string localesJson)
    {
        if (string.IsNullOrWhiteSpace(localesJson))
        {
            return ["default"];
        }

        try
        {
            using var document = JsonDocument.Parse(localesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ["default"];
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

            return locales.Count > 0 ? locales : ["default"];
        }
        catch (JsonException)
        {
            return ["default"];
        }
    }

    public static bool Contains(string localesJson, string locale) =>
        Parse(localesJson).Contains(locale, StringComparer.Ordinal);
}
