using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Features.Links;

internal readonly record struct SnapshotCell(
    long PropertyId,
    string VariableName,
    string Name,
    string DataType,
    string Value);

internal readonly record struct SnapshotHeader(
    long? AudienceLinkId,
    string Identifier,
    DateTime CapturedAt,
    string Source = PersonalizationSources.Link,
    long? MemberId = null);

/// <summary>
/// Snapshot v1 stored on the submission: <c>variables</c> (name → typed value) is what a survey
/// reads; <c>values[]</c> keeps each property's id, name and type beside its value, for reporting
/// and the codebook, because a frozen snapshot can never be enriched later.
/// </summary>
internal static class PersonalizationSnapshotJson
{
    internal const string SourceKey = "source";
    internal const string MemberIdKey = "memberId";

    public static string Write(SnapshotHeader header, IReadOnlyCollection<SnapshotCell> cells) =>
        Root(header, TypedVariables(cells), TypedValues(cells)).ToJsonString();

    /// <summary>
    /// Whether a stored snapshot names the participant whose audience member id is <paramref name="memberId"/>: an
    /// exact lookup of its top-level <c>memberId</c> key. Snapshots written before endatix#1213 have none.
    /// </summary>
    public static Expression<Func<string?, bool>> NamesParticipant(long memberId)
    {
        string id = memberId.ToString(CultureInfo.InvariantCulture);
        return snapshot => JsonDbFunctions.KeyText(snapshot, MemberIdKey) == id;
    }

    /// <summary>
    /// Whether a stored snapshot is from an on-behalf create: an exact lookup of its top-level <c>source</c> key. Used
    /// with <see cref="NamesParticipant"/> so an on-behalf submission still shows on the participant when the
    /// best-effort Submitter bind did not run.
    /// </summary>
    public static Expression<Func<string?, bool>> IsOnBehalf() =>
        snapshot => JsonDbFunctions.KeyText(snapshot, SourceKey) == PersonalizationSources.OnBehalf;

    private static JsonObject Root(SnapshotHeader header, JsonObject variables, JsonArray values)
    {
        JsonObject root = new()
        {
            ["schemaVersion"] = 1,
            [SourceKey] = header.Source,
            ["capturedAt"] = header.CapturedAt,
            ["identifier"] = header.Identifier,
            ["variables"] = variables,
            ["values"] = values,
        };
        Add(root, MemberIdKey, header.MemberId);
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

    private static JsonArray TypedValues(IEnumerable<SnapshotCell> cells)
    {
        JsonArray values = [];
        foreach (SnapshotCell cell in cells)
        {
            JsonNode? typed = Typed(cell.DataType, cell.Value);
            if (typed is not null)
            {
                values.Add(ValueEntry(cell, typed));
            }
        }

        return values;
    }

    private static JsonObject ValueEntry(SnapshotCell cell, JsonNode typed) => new()
    {
        ["propertyId"] = cell.PropertyId,
        ["variableName"] = cell.VariableName,
        ["name"] = cell.Name,
        ["dataType"] = cell.DataType,
        ["value"] = typed,
    };

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
