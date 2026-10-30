using System.Text.Encodings.Web;
using System.Text.Json;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Resolves CSV headers to property ids, including <c>variable__choice</c> choice columns. The
/// headers are resolved once per file, not once per cell.
/// </summary>
internal static class CsvColumnMapper
{
    private const string ChoiceSeparator = "__";
    private const string OtherSuffix = "other";
    private const string OtherTextSuffix = "other_text";

    /// <summary>
    /// The same JSON a browser writes for a multi-choice value (<c>["Café"]</c>, not
    /// <c>["Café"]</c>), so a value imported here equals one saved through the people API.
    /// </summary>
    private static readonly JsonSerializerOptions ChoiceJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static CsvColumnMap Build(CsvColumnRequest request)
    {
        Dictionary<string, Property> byVariable = request.Properties.ToDictionary(
            property => property.VariableName,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> simple = BuildSimpleColumns(byVariable, request.PropertyColumns);
        return new CsvColumnMap(
            request.IdentifierColumn,
            simple,
            BuildChoiceColumns(byVariable, request.Headers.Where(header => !simple.ContainsKey(header))),
            request.Properties.ToDictionary(property => property.Id));
    }

    /// <summary>
    /// Refuses a mapping that names a property this form lacks, a column the file lacks, or one
    /// column for two properties, so a typo cannot drop a whole column while the import reports
    /// success.
    /// </summary>
    public static string? MappingError(CsvColumnRequest request)
    {
        if (request.PropertyColumns is null)
        {
            return null;
        }

        HashSet<string> variables = request.Properties
            .Select(property => property.VariableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> columns = request.Headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return request.PropertyColumns
            .Select(column => ColumnError(variables, columns, column))
            .FirstOrDefault(error => error is not null)
            ?? SharedColumnError(request.PropertyColumns);
    }

    /// <summary>
    /// The row's values keyed by property id, or why the row cannot be read (a choice cell that is
    /// neither ticked nor unticked, two choices ticked for a single-choice property, Other text
    /// without Other).
    /// </summary>
    public static CellRead ReadValues(CsvColumnMap map, IReadOnlyDictionary<string, string> row)
    {
        Dictionary<long, string> values = ReadSimpleValues(map, row);
        ChoiceCells choices = new(values);
        foreach ((string header, ChoiceColumn column) in map.ChoiceColumns)
        {
            choices.Read(header, column, row.GetValueOrDefault(header) ?? string.Empty);
        }

        return choices.Finish(ChoiceJson);
    }

    private static string? ColumnError(
        HashSet<string> variables,
        HashSet<string> columns,
        KeyValuePair<string, string> column)
    {
        if (!variables.Contains(column.Key))
        {
            return $"This form has no audience property '{column.Key}'.";
        }

        return columns.Contains(column.Value) ? null : $"The CSV has no '{column.Value}' column.";
    }

    private static string? SharedColumnError(IReadOnlyDictionary<string, string> propertyColumns)
    {
        string? shared = propertyColumns.Values
            .GroupBy(header => header, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        return shared is null ? null : $"The CSV column '{shared}' is mapped to more than one property.";
    }

    private static Dictionary<string, long> BuildSimpleColumns(
        IReadOnlyDictionary<string, Property> byVariable,
        IReadOnlyDictionary<string, string>? propertyColumns)
    {
        Dictionary<string, long> simple = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string variableName, string csvHeader) in propertyColumns ?? new Dictionary<string, string>())
        {
            if (byVariable.TryGetValue(variableName, out Property? property))
            {
                simple[csvHeader] = property.Id;
            }
        }

        return simple;
    }

    /// <summary>
    /// Each <c>variable__choice</c> header whose variable is a choice property, except headers the
    /// caller mapped to a property. Other headers with <c>__</c> are ordinary columns, because a
    /// column name may contain it.
    /// </summary>
    private static Dictionary<string, ChoiceColumn> BuildChoiceColumns(
        IReadOnlyDictionary<string, Property> byVariable,
        IEnumerable<string> headers)
    {
        Dictionary<string, ChoiceColumn> choices = new(StringComparer.OrdinalIgnoreCase);
        foreach (string header in headers)
        {
            ChoiceColumn? column = ChoiceColumnOf(byVariable, header);
            if (column is not null)
            {
                choices[header] = column;
            }
        }

        return choices;
    }

    private static ChoiceColumn? ChoiceColumnOf(IReadOnlyDictionary<string, Property> byVariable, string header)
    {
        int separator = header.IndexOf(ChoiceSeparator, StringComparison.Ordinal);
        if (separator <= 0
            || !byVariable.TryGetValue(header[..separator], out Property? property)
            || !IsChoice(property))
        {
            return null;
        }

        string choice = header[(separator + ChoiceSeparator.Length)..];
        if (property.DataType == AudienceDataTypeCodes.MultipleChoice)
        {
            return MultiChoiceColumn(property, choice);
        }

        return IsUnlistedOtherText(property, choice) ? null : new ChoiceColumn(property, CanonicalKey(property, choice));
    }

    /// <summary>
    /// <c>__other</c> and <c>__other_text</c> are the Other tick and its text unless the property
    /// lists a key of that name. A listed <c>other</c> key stays that key; on a property without
    /// Other, <c>__other</c> sets the key <c>other</c>, which the value check then refuses.
    /// </summary>
    private static ChoiceColumn MultiChoiceColumn(Property property, string choice)
    {
        if (IsUnlistedOtherText(property, choice))
        {
            return new ChoiceColumn(property, null, ChoiceColumnRole.OtherText);
        }

        string key = CanonicalKey(property, choice);
        if (!key.Equals(OtherSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return new ChoiceColumn(property, key);
        }

        return property.AllowsOther && !property.ChoiceKeys().Contains(key)
            ? new ChoiceColumn(property, null, ChoiceColumnRole.FreeOther)
            : new ChoiceColumn(property, key, ChoiceColumnRole.Other);
    }

    /// <summary>
    /// The export's Other text column. A single-choice property skips it: the import does not
    /// store a single choice's Other text yet, and reading it as a tick would refuse the export.
    /// </summary>
    private static bool IsUnlistedOtherText(Property property, string choice) =>
        choice.Equals(OtherTextSuffix, StringComparison.OrdinalIgnoreCase)
        && !property.ChoiceKeys().Contains(CanonicalKey(property, choice));

    /// <summary>
    /// The property's own spelling of the key when the header differs only in case (an export tool
    /// lower-cased <c>region__North</c>); otherwise the header's text, which the value check then
    /// refuses unless the property allows other values.
    /// </summary>
    private static string CanonicalKey(Property property, string choice) =>
        property.ChoiceKeys().FirstOrDefault(key => key.Equals(choice, StringComparison.OrdinalIgnoreCase))
        ?? choice;

    private static bool IsChoice(Property property) =>
        property.DataType is AudienceDataTypeCodes.SingleChoice or AudienceDataTypeCodes.MultipleChoice;

    private static Dictionary<long, string> ReadSimpleValues(
        CsvColumnMap map,
        IReadOnlyDictionary<string, string> row)
    {
        Dictionary<long, string> values = new();
        foreach ((string header, long propertyId) in map.SimpleColumns)
        {
            if (row.TryGetValue(header, out string? cell) && !string.IsNullOrWhiteSpace(cell))
            {
                values[propertyId] = cell.Trim();
            }
        }

        return values;
    }
}

/// <summary>
/// What the column mapping is built from: the form's properties, the file's headers, and the
/// caller's choices.
/// </summary>
internal sealed record CsvColumnRequest(
    IReadOnlyList<Property> Properties,
    IReadOnlyList<string> Headers,
    string IdentifierColumn,
    IReadOnlyDictionary<string, string>? PropertyColumns);

/// <summary>
/// A <c>variable__choice</c> header: the choice property it fills, the key a ticked cell sets
/// (none for <see cref="ChoiceColumnRole.FreeOther"/> and <see cref="ChoiceColumnRole.OtherText"/>),
/// and its role.
/// </summary>
internal sealed record ChoiceColumn(
    Property Property,
    string? ChoiceKey,
    ChoiceColumnRole Role = ChoiceColumnRole.Choice);

internal enum ChoiceColumnRole
{
    /// <summary>A ticked cell sets the key.</summary>
    Choice,

    /// <summary><c>__other</c> naming a key (listed, or refused where Other is not allowed); the text is optional.</summary>
    Other,

    /// <summary><c>__other</c> on a property that allows Other and lists no such key: it sets no key and needs the text.</summary>
    FreeOther,

    /// <summary><c>__other_text</c>: free text, stored as a choice when the Other tick allows it.</summary>
    OtherText,
}

/// <summary>
/// One row's values keyed by property id, or the reason the row cannot be read.
/// </summary>
internal sealed record CellRead(IReadOnlyDictionary<long, string> Values, string? Error);

internal sealed record CsvColumnMap(
    string IdentifierColumn,
    IReadOnlyDictionary<string, long> SimpleColumns,
    IReadOnlyDictionary<string, ChoiceColumn> ChoiceColumns,
    IReadOnlyDictionary<long, Property> PropertiesById);
