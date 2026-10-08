using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features.Links;

internal readonly record struct SnapshotCell(string VariableName, string DataType, string Value);

/// <summary>
/// Snapshot v1 stored on the submission. Variables are already JSON-typed.
/// </summary>
internal static class PersonalizationSnapshotJson
{
    public static string Write(
        long audienceLinkId,
        string identifier,
        DateTime capturedAt,
        IEnumerable<SnapshotCell> cells)
    {
        JsonObject variables = new();
        foreach (SnapshotCell cell in cells)
        {
            JsonNode? typed = Typed(cell.DataType, cell.Value);
            if (typed is not null)
            {
                variables[cell.VariableName] = typed;
            }
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capturedAt"] = capturedAt,
            ["identifier"] = identifier,
            ["audienceLinkId"] = audienceLinkId,
            ["variables"] = variables,
        }.ToJsonString();
    }

    internal static JsonNode? Typed(string dataType, string value)
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
            return JsonNode.Parse(value);
        }

        return JsonValue.Create(value);
    }
}
