using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features;

/// <summary>
/// The one place a stored cell value becomes the JSON a survey reads, and back. The respondent's Snapshot and the
/// designer's variables both type through <see cref="Typed"/>, so a value previewed equals the value frozen.
/// </summary>
internal static class AudienceValueJson
{
    /// <summary>Choice keys are stored as written, not escaped; the cell is data, never HTML.</summary>
    private static readonly JsonSerializerOptions StoredJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A number, a boolean or a choice list as JSON, anything else as a string; an empty cell is null.</summary>
    public static JsonNode? Typed(string dataType, string value)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (dataType == AudienceDataTypeCodes.Number
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
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
    /// The stored text of a typed value: a string as itself, any other JSON as its compact text, null as empty. The JSON
    /// kind already says the type, so no data type is needed. <c>Typed(t, Untyped(Typed(t, v)))</c> equals
    /// <c>Typed(t, v)</c> for every value <c>Property.ValueError</c> accepts, and <c>Untyped(Typed(t, v))</c> is
    /// <c>v</c> itself when <c>v</c> is written canonically (<c>1.5</c>, not <c>1.50</c>; <c>["a","b"]</c>).
    /// </summary>
    public static string Untyped(JsonNode? value) =>
        value switch
        {
            null => string.Empty,
            JsonValue text when text.GetValueKind() == JsonValueKind.String => text.GetValue<string>(),
            _ => value.ToJsonString(StoredJson),
        };

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
