using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>One frozen audience column. <see cref="Name"/> is the snapshot key; <see cref="ExportKey"/> is the CSV header.</summary>
internal readonly record struct AudienceColumn(string Name, string ExportKey);

/// <summary>
/// Audience columns for a submission export. Values come from the snapshot frozen at open,
/// not from the live audience and not from the survey model's variables.
/// </summary>
internal static class AudienceExport
{
    public const string Identifier = "identifier";
    public const string Prefix = "audience_";

    public static IReadOnlyList<AudienceColumn> Columns(
        IEnumerable<string?> snapshots,
        IReadOnlySet<string> questionKeys)
    {
        SortedSet<string> names = new(StringComparer.Ordinal);
        var sawSnapshot = false;
        foreach (string? snapshot in snapshots)
        {
            var read = PersonalizationReader.Read(snapshot);
            if (read is null)
            {
                continue;
            }

            sawSnapshot = true;
            foreach (JsonProperty property in read.Variables.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        if (!sawSnapshot)
        {
            return [];
        }

        HashSet<string> taken = new(questionKeys, StringComparer.Ordinal);
        List<AudienceColumn> columns = [new(Identifier, Header(Identifier, taken))];
        foreach (string name in names)
        {
            columns.Add(new AudienceColumn(name, Header(name, taken)));
        }

        return columns;
    }

    /// <summary>Writes snapshot values into the answers object under each column's export key.</summary>
    public static string Merge(string? answersJson, string? snapshot, IReadOnlyList<AudienceColumn> columns)
    {
        if (columns.Count == 0)
        {
            return answersJson ?? "{}";
        }

        JsonObject root = ParseObject(answersJson);
        var read = PersonalizationReader.Read(snapshot);
        if (read is null)
        {
            return root.ToJsonString();
        }

        foreach (AudienceColumn column in columns)
        {
            if (column.Name == Identifier)
            {
                if (read.Identifier.Length > 0)
                {
                    root[column.ExportKey] = read.Identifier;
                }

                continue;
            }

            if (read.Variables.TryGetProperty(column.Name, out JsonElement value))
            {
                root[column.ExportKey] = JsonNode.Parse(value.GetRawText());
            }
        }

        return root.ToJsonString();
    }

    private static string Header(string name, HashSet<string> taken)
    {
        string header = taken.Contains(name) ? Prefix + name : name;
        taken.Add(header);
        return header;
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
