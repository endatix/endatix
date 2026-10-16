using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <summary>
/// Schema columns for the audience values frozen on a response. A form gets a column only for a variable that a
/// flattened response actually carries, so properties nobody was ever personalised with never reach the schema.
/// </summary>
/// <remarks>
/// Keys share the schema with question keys, and the append-only merge keeps the first column a key had: a question
/// named like an audience key (<c>audience_city</c>) keeps that key and the audience value is not exported (#1230).
/// Exports and Shoji let the question win the same way when only its export key matches. Property variable names are
/// lowercase slugs, so they can never produce <see cref="IdentifierKey"/>, which follows the system columns' casing.
/// </remarks>
internal static class PersonalizationColumns
{
    public const string IdentifierKey = "AudienceIdentifier";
    public const string VariablePrefix = "audience_";
    public const string IdentifierLabel = "Audience identifier";

    // Audience data type codes as the snapshot writes them (AudienceDataTypeCodes in the Audience contracts).
    public const string TextType = "text";
    public const string NumberType = "number";
    public const string BooleanType = "boolean";
    public const string DateType = "date";
    public const string DateTimeType = "date_time";
    private const string MultipleChoiceType = "multiple_choice";

    /// <summary>The identifier column, then one column per frozen variable, in variable-name order.</summary>
    public static IReadOnlyList<FormSchemaColumn> From(PersonalizationRead? read)
    {
        if (read is null)
        {
            return [];
        }

        Dictionary<string, PersonalizationValue> described = DescribedValues(read);
        List<FormSchemaColumn> columns = [Identifier()];
        foreach (var variable in read.Variables.EnumerateObject().OrderBy(variable => variable.Name, StringComparer.Ordinal))
        {
            columns.Add(Variable(variable, described.GetValueOrDefault(variable.Name)));
        }

        return columns;
    }

    public static string KeyOf(string variableName) => VariablePrefix + variableName;

    /// <summary>The frozen value a column holds for this response; null when the response has none.</summary>
    public static JsonElement? ValueOf(FormSchemaColumn column, PersonalizationRead? read)
    {
        if (read is null)
        {
            return null;
        }

        if (column.Key == IdentifierKey)
        {
            return read.Identifier.Length == 0 ? null : JsonSerializer.SerializeToElement(read.Identifier);
        }

        var variableName = column.Key[VariablePrefix.Length..];
        return read.Variables.TryGetProperty(variableName, out var value) ? value.Clone() : null;
    }

    /// <summary>Whether the schema already has every column, with a type that holds the value.</summary>
    public static bool AreCoveredBy(MergedFormSchema schema, IReadOnlyList<FormSchemaColumn> columns) =>
        columns.All(column => schema.TryGetColumn(column.Key, out var existing) && Holds(existing, column));

    /// <summary>
    /// Appends the missing columns. A variable frozen with two data types (a property deleted and re-created under
    /// the same name) becomes text, which holds both.
    /// </summary>
    public static MergedFormSchema IncludeIn(
        MergedFormSchema schema,
        IReadOnlyList<FormSchemaColumn> columns,
        SchemaCompilationLimits? limits = null)
    {
        Dictionary<string, FormSchemaColumn> incoming = columns.ToDictionary(column => column.Key, StringComparer.Ordinal);
        MergedFormSchema widened = new(schema.Columns.Select(existing => Widened(existing, incoming)));
        return widened.MergeAppendOnly(columns, limits);
    }

    /// <summary>The audience columns a persisted flattening map already has.</summary>
    public static IReadOnlyList<FormSchemaColumn> In(string flatteningMapJson) =>
        FormSchemaFlatteningMap.FromJson(flatteningMapJson).Columns
            .Where(column => column.Kind is FormSchemaColumnKind.Personalization)
            .ToList();

    private static Dictionary<string, PersonalizationValue> DescribedValues(PersonalizationRead read)
    {
        Dictionary<string, PersonalizationValue> described = new(StringComparer.Ordinal);
        foreach (var value in read.Values)
        {
            described.TryAdd(value.VariableName, value);
        }

        return described;
    }

    private static FormSchemaColumn Identifier() =>
        new(IdentifierKey, FormSchemaColumnKind.Personalization, IdentifierLabel, TextType);

    // A snapshot written before values[] existed has no name or type; the JSON kind of the value stands in for the type.
    private static FormSchemaColumn Variable(JsonProperty variable, PersonalizationValue? described) =>
        new(
            KeyOf(variable.Name),
            FormSchemaColumnKind.Personalization,
            described?.Name is { Length: > 0 } name ? name : variable.Name,
            described?.DataType ?? TypeOf(variable.Value));

    private static string TypeOf(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number => NumberType,
            JsonValueKind.True or JsonValueKind.False => BooleanType,
            JsonValueKind.Array => MultipleChoiceType,
            _ => TextType,
        };

    private static bool Holds(FormSchemaColumn existing, FormSchemaColumn column) =>
        existing.Kind is not FormSchemaColumnKind.Personalization ||
        existing.DataType == column.DataType ||
        existing.DataType == TextType;

    private static FormSchemaColumn Widened(FormSchemaColumn existing, IReadOnlyDictionary<string, FormSchemaColumn> incoming) =>
        incoming.TryGetValue(existing.Key, out var column) && !Holds(existing, column)
            ? existing with { DataType = TextType }
            : existing;
}
