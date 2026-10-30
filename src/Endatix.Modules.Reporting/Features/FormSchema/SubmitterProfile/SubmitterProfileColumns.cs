using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;

/// <summary>
/// Schema columns for the submitter profile fields a response carries, read from its profile snapshot, not its
/// answers. Every compile adds the configured fields' columns, so a replace keeps them like the definition's own.
/// </summary>
/// <remarks>
/// Keys share the schema with question keys, and the first column a key had wins: a question named like a submitter
/// key (<c>submitter_email</c>) keeps that key and the profile value is not exported, as with audience keys.
/// </remarks>
internal static class SubmitterProfileColumns
{
    public const string KeyPrefix = "submitter_";
    public const string DataType = "string";
    private const string LabelPrefix = "Submitter ";

    public static FormSchemaColumn Column(string field) =>
        new(KeyOf(field), FormSchemaColumnKind.SubmitterProfile, LabelPrefix + field, DataType);

    public static string KeyOf(string field) => KeyPrefix + field;

    /// <summary>The value a column holds for this response; null when the profile does not have the field.</summary>
    public static JsonElement? ValueOf(FormSchemaColumn column, IReadOnlyDictionary<string, string>? profile) =>
        profile is not null && profile.TryGetValue(column.Key[KeyPrefix.Length..], out var value)
            ? JsonSerializer.SerializeToElement(value)
            : null;

    /// <summary>The submitter profile columns as one block, in schema order.</summary>
    public static IEnumerable<FormSchemaColumn> InBlockOrder(IEnumerable<FormSchemaColumn> columns) =>
        columns.Where(column => column.Kind is FormSchemaColumnKind.SubmitterProfile);
}
