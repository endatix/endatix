using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>Shoji variables for the submitter profile fields captured with each response.</summary>
internal static partial class ShojiCodebookGenerator
{
    private const string SubmitterProfileDescription = "Submitter profile value captured with the response.";

    // Text variables, whatever the claim holds: the snapshot keeps every field as a string. The alias is the column
    // key as stored, the CSV header, as for audience variables; an alias a question already took stays the question's.
    /// <returns>The aliases of the submitter variables written.</returns>
    private static List<string> WriteSubmitterProfileVariables(
        Utf8JsonWriter writer,
        ShojiCodebookWriteArgs args,
        ShojiVariableTracking tracking)
    {
        var firstSubmitterProfile = tracking.OrderAliases.Count;
        foreach (var column in SubmitterProfileColumns.InBlockOrder(args.FlatteningMap.Columns))
        {
            if (TryTrackVariable(tracking.WrittenVariables, tracking.OrderAliases, column.Key))
            {
                args.CodebookColumns.TryGetValue(column.Key, out var columnMetadata);
                SubmitterProfileVariable variable = new(column.Key, ReadColumnTitle(columnMetadata, column.Label));
                WriteSubmitterProfileVariable(writer, variable, tracking.UsedDisplayNames);
            }
        }

        return tracking.OrderAliases[firstSubmitterProfile..];
    }

    private sealed record SubmitterProfileVariable(string Alias, string DisplayName);

    private static void WriteSubmitterProfileVariable(
        Utf8JsonWriter writer,
        SubmitterProfileVariable variable,
        HashSet<string> usedDisplayNames)
    {
        writer.WritePropertyName(variable.Alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, variable.Alias);
        WriteUniqueName(writer, variable.DisplayName, variable.Alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, SubmitterProfileDescription);
        writer.WriteEndObject();
    }
}
