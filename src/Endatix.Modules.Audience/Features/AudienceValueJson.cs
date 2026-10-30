using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features;

/// <summary>
/// The one place a stored cell value becomes the JSON a survey reads, and back. The respondent's Snapshot and the
/// designer's variables both type through <see cref="Typed"/>, so a value previewed equals the value frozen; the
/// designer's edits are stored through <see cref="Untyped"/>.
/// </summary>
internal static class AudienceValueJson
{
    /// <summary>Compact, and non-ASCII text as written, as a client sends a choice list.</summary>
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// A number, a boolean or a choice list as JSON, anything else as a string; an empty cell is null. A non-finite
    /// number stays a string, because JSON has no NaN or Infinity and serializing one throws.
    /// </summary>
    public static JsonNode? Typed(string dataType, string value)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (dataType == AudienceDataTypeCodes.Number
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            return JsonValue.Create(number);
        }

        if (dataType == AudienceDataTypeCodes.Boolean && value is "true" or "false")
        {
            return JsonValue.Create(value == "true");
        }

        if (dataType == AudienceDataTypeCodes.MultipleChoice)
        {
            return Choice(value);
        }

        return JsonValue.Create(value);
    }

    /// <summary>
    /// The cell text for a value a survey holds: null is empty (clears the cell), a string is its text, a number is
    /// written in one form (<c>1.50</c> and <c>"1.5"</c> both become <c>1.5</c>), and anything else is compact JSON.
    /// <c>Untyped(t, Typed(t, v)) == v</c> for every value in that one form, and <c>Typed</c> of the result always equals
    /// <c>Typed</c> of the input, so comparing two values through it ignores how a number was written. The result is not
    /// checked: <see cref="Domain.PropertyValueRules.ValueError"/> does that.
    /// </summary>
    public static string Untyped(string dataType, JsonNode? value)
    {
        if (value is not JsonValue scalar)
        {
            return value?.ToJsonString(Compact) ?? "";
        }

        string text = scalar.GetValueKind() == JsonValueKind.String
            ? scalar.GetValue<string>()
            : scalar.ToJsonString(Compact);
        return dataType == AudienceDataTypeCodes.Number
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number)
                ? number.ToString("R", CultureInfo.InvariantCulture)
                : text;
    }

    /// <summary>A stored choice list is JSON. Unparseable text is kept as text so one bad cell cannot block the open.</summary>
    private static JsonNode? Choice(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }
}
