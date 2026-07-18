using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class SurveyJsJsonElementExtensions
{
    /// <summary>
    /// Resolves flattening for an element, honoring <c>imagepicker.multiSelect</c>.
    /// </summary>
    public static SurveyJsFlattening ResolveSurveyJsFlattening(this JsonElement element)
    {
        if (SurveyJsElementType.ImagePicker.Matches(element.GetSurveyJsType()) &&
            element.GetBooleanProperty(SurveyJsPropertyNames.MultiSelect))
        {
            return SurveyJsFlattening.ChoiceIndicators;
        }

        return SurveyJsElementType.ResolveFlattening(element.GetSurveyJsType());
    }

    public static bool IsMultiSelectBaseSelect(this JsonElement element)
    {
        var type = element.GetSurveyJsType();
        if (SurveyJsElementType.Checkbox.Matches(type) || SurveyJsElementType.Tagbox.Matches(type))
        {
            return true;
        }

        return SurveyJsElementType.ImagePicker.Matches(type) &&
               element.GetBooleanProperty(SurveyJsPropertyNames.MultiSelect);
    }

    public static bool IsSingleSelectBaseSelect(this JsonElement element) =>
        SurveyJsElementType.TryResolve(element.GetSurveyJsType()) is
            { Category: SurveyJsElementCategory.BaseSelect } &&
        !element.IsMultiSelectBaseSelect();

    public static bool IsRangeSlider(this JsonElement element) =>
        SurveyJsElementType.Slider.Matches(element.GetSurveyJsType()) &&
        string.Equals(
            element.GetStringProperty(SurveyJsPropertyNames.SliderType),
            SurveyJsPropertyNames.SliderTypeRange,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsDateInputType(this JsonElement element)
    {
        var inputType = element.GetStringProperty(SurveyJsPropertyNames.InputType);
        return string.Equals(inputType, "date", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(inputType, "datetime", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(inputType, "datetime-local", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a string property, or <c>null</c> if the property is missing or not a string.
    /// </summary>
    public static string? GetStringProperty(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    /// <summary>
    /// Gets a non-empty string property, or <c>null</c> if the property is missing or empty.
    /// </summary>
    public static string? GetNonEmptyStringProperty(this JsonElement element, string propertyName)
    {
        var value = element.GetStringProperty(propertyName);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Gets a non-empty string value, or <c>null</c> if the value is not a string or is empty.
    /// </summary>
    public static string? GetNonEmptyStringValue(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Normalizes a scalar submission value to the same string form used for compiled choice keys.
    /// </summary>
    public static string? GetScalarStringValue(this JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetNonEmptyStringValue(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };

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

    /// <summary>
    /// Tries to get an int32 property, or <c>false</c> if the property is missing or not a number.
    /// </summary>
    public static bool TryGetInt32Property(this JsonElement element, string propertyName, out int value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets an int32 property, or <c>null</c> if the property is missing or not a number.
    /// </summary>
    public static int? GetNullableInt32Property(this JsonElement element, string propertyName) =>
        element.TryGetInt32Property(propertyName, out var value) ? value : null;

    /// <summary>
    /// Gets an enum property, or <c>null</c> if the property is missing or not a string.
    /// </summary>
    public static bool TryGetEnumProperty<TEnum>(this JsonElement element, string propertyName, out TEnum value)
        where TEnum : struct, Enum
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            Enum.TryParse(property.GetString(), ignoreCase: true, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the property is missing or JSON null (out <paramref name="array"/> is null).
    /// Returns <c>false</c> when the property exists but is not an array.
    /// </summary>
    public static bool TryGetNullableArrayProperty(
        this JsonElement element,
        string propertyName,
        out JsonElement? array)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            array = null;
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            array = null;
            return false;
        }

        array = property;
        return true;
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

    /// <summary>
    /// Attempts to retrieve the <c>loopSource</c> array property. Returns <c>true</c> with the array element on success.
    /// </summary>
    public static bool TryGetLoopSource(this JsonElement element, out JsonElement loopSource) =>
        element.TryGetArrayProperty(SurveyJsPropertyNames.LoopSource, out loopSource);
}
