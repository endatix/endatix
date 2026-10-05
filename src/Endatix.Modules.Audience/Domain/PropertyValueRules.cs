using System.Globalization;
using System.Text.Json;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// Checks choice settings against a data type, and a cell value against its property, so
/// prefill and piping only ever read values that parse.
/// </summary>
internal static class PropertyValueRules
{
    private const NumberStyles CanonicalNumber =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    /// <summary>
    /// One check per data type. Each returns the error tail, or null when the value fits.
    /// </summary>
    private static readonly Dictionary<string, Func<Property, string, string?>> Checks = new(StringComparer.Ordinal)
    {
        [AudienceDataTypeCodes.Number] = (_, value) => IsNumber(value) ? null : "must be a number",
        [AudienceDataTypeCodes.Boolean] = (_, value) => value is "true" or "false" ? null : "must be true or false",
        [AudienceDataTypeCodes.Date] = (_, value) => IsDate(value) ? null : "must be a date (YYYY-MM-DD)",
        [AudienceDataTypeCodes.DateTime] = (_, value) => IsDateTime(value) ? null : "must be an ISO 8601 date and time",
        [AudienceDataTypeCodes.SingleChoice] = SingleChoiceError,
        [AudienceDataTypeCodes.MultipleChoice] = MultipleChoiceError,
    };

    public static bool IsChoiceType(string dataType) =>
        dataType is AudienceDataTypeCodes.SingleChoice or AudienceDataTypeCodes.MultipleChoice;

    /// <summary>
    /// Choice types need a JSON array of distinct, non-blank keys. Other types take no choices.
    /// </summary>
    public static string? ChoicesError(string dataType, string? choicesJson, bool allowsOther)
    {
        if (IsChoiceType(dataType))
        {
            return KeysError(ParseKeys(choicesJson));
        }

        return choicesJson is null && !allowsOther
            ? null
            : "Only single choice and multiple choice properties take choices.";
    }

    /// <summary>
    /// An empty string clears the cell and is always allowed.
    /// </summary>
    public static string? ValueError(Property property, string value)
    {
        if (value.Length == 0)
        {
            return null;
        }

        string? error = Checks.TryGetValue(property.DataType, out Func<Property, string, string?>? check)
            ? check(property, value)
            : null;
        return error is null ? null : $"'{property.Name}' {error}.";
    }

    public static List<string>? ParseKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? KeysError(List<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return "Choices must be a JSON array with at least one choice key.";
        }

        if (keys.Any(string.IsNullOrWhiteSpace))
        {
            return "Choice keys cannot be blank.";
        }

        return keys.Distinct(StringComparer.Ordinal).Count() == keys.Count
            ? null
            : "Choice keys must be unique.";
    }

    private static string? SingleChoiceError(Property property, string value) =>
        IsAllowedChoice(property, value) ? null : $"has no choice '{value}'";

    private static string? MultipleChoiceError(Property property, string value)
    {
        List<string>? picked = ParseKeys(value);
        if (picked is null)
        {
            return "must be a JSON array of choice keys";
        }

        string? unknown = picked.FirstOrDefault(key => !IsAllowedChoice(property, key));
        return unknown is null ? null : $"has no choice '{unknown}'";
    }

    /// <summary>
    /// A data-list property gets its choices from the list (not checked here yet).
    /// </summary>
    private static bool IsAllowedChoice(Property property, string key) =>
        property.AllowsOther
        || property.DataListId is not null
        || property.ChoiceKeys().Contains(key);

    /// <summary>
    /// No surrounding whitespace, so the stored text is the number as written. <c>double</c>
    /// covers large values; infinity (for example <c>1e400</c>) is refused.
    /// </summary>
    private static bool IsNumber(string value) =>
        double.TryParse(value, CanonicalNumber, CultureInfo.InvariantCulture, out double number)
        && double.IsFinite(number);

    private static bool IsDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsDateTime(string value) =>
        DateTimeOffset.TryParseExact(
            value,
            DateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out _);
}
