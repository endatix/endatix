using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Features.SubmissionViews;

/// <summary>
/// Drops the saved columns a form no longer has, so a question removed from the form leaves no ghost column.
/// </summary>
/// <remarks>
/// Applied to what a read or a save returns, never to what is stored: the stored settings keep every column, so a
/// question that comes back keeps its width. <see cref="SubmissionViewReader"/> applies it.
/// </remarks>
internal static class SubmissionViewColumns
{
    /// <summary>
    /// The grid's own columns, which come from the submission rather than the form schema. Keys match the Hub grid's
    /// column ids.
    /// </summary>
    internal static readonly IReadOnlySet<string> MetadataKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "complete",
        "createdAt",
        "modifiedAt",
        "startedAt",
        "completedAt",
        "completionTime",
        "submitterDisplayId",
        "status",
    };

    /// <summary>
    /// Returns <paramref name="settingsJson"/> without the columns whose key is neither in
    /// <paramref name="schemaKeys"/> nor one of <see cref="MetadataKeys"/>; unchanged when every key is known.
    /// </summary>
    internal static string KeepKnown(string settingsJson, IReadOnlySet<string> schemaKeys)
    {
        var root = JsonNode.Parse(settingsJson);
        if (root?[SubmissionViewSettings.ColumnsProperty] is not JsonArray columns)
        {
            return settingsJson;
        }

        var dropped = columns.RemoveAll(column => !IsKnown(column, schemaKeys));
        return dropped == 0 ? settingsJson : root.ToJsonString();
    }

    private static bool IsKnown(JsonNode? column, IReadOnlySet<string> schemaKeys) =>
        column?[SubmissionViewSettings.KeyProperty] is JsonValue value &&
        value.TryGetValue<string>(out var key) &&
        (schemaKeys.Contains(key) || MetadataKeys.Contains(key));
}
