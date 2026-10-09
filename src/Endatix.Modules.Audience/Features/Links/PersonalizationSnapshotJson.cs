using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features.Links;

internal readonly record struct SnapshotCell(string VariableName, string DataType, string Value);

internal readonly record struct SnapshotHeader(
    long? AudienceLinkId,
    string Identifier,
    DateTime CapturedAt,
    string Source = "link",
    long? MemberId = null);

/// <summary>
/// Snapshot v1 stored on the submission. Variables are already JSON-typed.
/// </summary>
internal static class PersonalizationSnapshotJson
{
    public static string Write(SnapshotHeader header, IEnumerable<SnapshotCell> cells) =>
        Root(header, TypedVariables(cells)).ToJsonString();

    private static JsonObject Root(SnapshotHeader header, JsonObject variables)
    {
        JsonObject root = new()
        {
            ["schemaVersion"] = 1,
            ["source"] = header.Source,
            ["capturedAt"] = header.CapturedAt,
            ["identifier"] = header.Identifier,
            ["variables"] = variables,
        };
        Add(root, "memberId", header.MemberId);
        Add(root, "audienceLinkId", header.AudienceLinkId);
        return root;
    }

    private static void Add(JsonObject root, string name, long? value)
    {
        if (value is long number)
        {
            root[name] = number;
        }
    }

    private static JsonObject TypedVariables(IEnumerable<SnapshotCell> cells)
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

        return variables;
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
