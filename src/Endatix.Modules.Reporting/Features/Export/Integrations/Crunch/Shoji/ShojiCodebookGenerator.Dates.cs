using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal static partial class ShojiCodebookGenerator
{
    private const string DAY_RESOLUTION = "D";

    private static void WriteQuestionDate(Utf8JsonWriter writer, string type, JsonElement question)
    {
        if (type != ShojiCodebookPropertyNames.VariableTypeDatetime)
        {
            return;
        }

        var inputType = question.ValueKind == JsonValueKind.Object ? question.GetStringProperty(SurveyJsPropertyNames.InputType) : null;
        WriteDateFormat(writer, string.Equals(inputType, "date", StringComparison.OrdinalIgnoreCase)
            ? DAY_RESOLUTION : ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
    }

    private static void WriteDateFormat(Utf8JsonWriter writer, string resolution)
    {
        var format = resolution == DAY_RESOLUTION ? "%Y-%m-%d" : "%Y-%m-%d %H:%M";
        writer.WriteString(ShojiCodebookPropertyNames.Resolution, resolution);
        writer.WriteStartObject("format");
        writer.WriteString("data", format);
        writer.WriteString("summary", format);
        writer.WriteEndObject();
        if (resolution == ShojiCodebookPropertyNames.DatetimeResolutionSeconds)
        {
            writer.WriteStartObject("view");
            writer.WriteString("rollup_resolution", "h");
            writer.WriteEndObject();
        }
    }
}
