using System.Text.Json;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class SurveyJsJsonElementExtensions
{
    public static string? GetStringProperty(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    public static string? GetNonEmptyStringProperty(this JsonElement element, string propertyName)
    {
        var value = element.GetStringProperty(propertyName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? GetNonEmptyStringValue(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static JsonElement? TryGetPropertyValue(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value;
    }

    public static bool TryGetArrayProperty(this JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.TryGetProperty(propertyName, out array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    public static bool TryGetInt32Property(this JsonElement element, string propertyName, out int value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            value = property.GetInt32();
            return true;
        }

        value = default;
        return false;
    }

    public static bool GetBooleanProperty(this JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue,
        };
    }

    /// <summary>
    /// Gets the SurveyJS <c>type</c> property as a string, or <c>null</c> if missing or not a string.
    /// </summary>
    public static string? GetSurveyJsType(this JsonElement element) =>
        element.GetStringProperty(SurveyJsPropertyNames.Type);

    /// <summary>
    /// Gets the SurveyJS <c>name</c> property as a string, or <c>null</c> if missing or not a string.
    /// </summary>
    public static string? GetSurveyJsName(this JsonElement element) =>
        element.GetStringProperty(SurveyJsPropertyNames.Name);

    /// <summary>
    /// Gets the SurveyJS <c>title</c> property as a string, falling back to <paramref name="fallback"/> if the title is missing or empty.
    /// </summary>
    public static string GetSurveyJsTitle(this JsonElement element, string fallback)
    {
        var title = element.GetStringProperty(SurveyJsPropertyNames.Title);
        return string.IsNullOrWhiteSpace(title) ? fallback : title;
    }

    /// <summary>
    /// Gets the SurveyJS <c>valueName</c> property as a string, or <c>null</c> if missing or not a string.
    /// </summary>
    public static string? GetSurveyJsValueName(this JsonElement element) =>
        element.GetStringProperty(SurveyJsPropertyNames.ValueName);

    /// <summary>
    /// Attempts to retrieve the <c>pages</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetPages(this JsonElement element, out JsonElement pages) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out pages);

    /// <summary>
    /// Attempts to retrieve the <c>elements</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetElements(this JsonElement element, out JsonElement elements) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Elements, out elements);

    /// <summary>
    /// Attempts to retrieve the <c>templateElements</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetTemplateElements(this JsonElement element, out JsonElement templateElements) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.TemplateElements, out templateElements);

    /// <summary>
    /// Attempts to retrieve the <c>choices</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetChoices(this JsonElement element, out JsonElement choices) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out choices);

    /// <summary>
    /// Attempts to retrieve the <c>rows</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetRows(this JsonElement element, out JsonElement rows) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Rows, out rows);

    /// <summary>
    /// Attempts to retrieve the <c>columns</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetColumns(this JsonElement element, out JsonElement columns) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Columns, out columns);

    /// <summary>
    /// Attempts to retrieve the <c>items</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetItems(this JsonElement element, out JsonElement items) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.Items, out items);

    /// <summary>
    /// Attempts to retrieve the <c>calculatedValues</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetCalculatedValues(this JsonElement element, out JsonElement calculatedValues) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.CalculatedValues, out calculatedValues);
}
