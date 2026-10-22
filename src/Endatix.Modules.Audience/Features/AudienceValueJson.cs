using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features;

/// <summary>
/// The one place a stored cell value becomes the JSON a survey reads. The respondent's Snapshot and the
/// designer's variables both type through <see cref="Typed"/>, so a value previewed equals the value frozen.
/// </summary>
internal static class AudienceValueJson
{
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
