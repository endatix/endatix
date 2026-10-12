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
        return new CsvColumnMap(
            request.IdentifierColumn,
            BuildSimpleColumns(byVariable, request.PropertyColumns),
            BuildChoiceColumns(byVariable, request.Headers),
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
    /// The row's values keyed by property id, or why the row cannot be read (two choices ticked
    /// for a single-choice property).
    /// </summary>
    public static CellRead ReadValues(CsvColumnMap map, IReadOnlyDictionary<string, string> row)
    {
        Dictionary<long, string> values = ReadSimpleValues(map, row);
        ChoiceCells choices = new(values);
        foreach ((string header, ChoiceColumn column) in map.ChoiceColumns)
        {
            if (row.TryGetValue(header, out string? cell) && IsTruthy(cell))
            {
                choices.Tick(column);
            }
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
    /// Each <c>variable__choice</c> header whose variable is a choice property. Other headers with
    /// <c>__</c> are ordinary columns, because a column name may contain it.
    /// </summary>
    private static Dictionary<string, ChoiceColumn> BuildChoiceColumns(
        IReadOnlyDictionary<string, Property> byVariable,
        IReadOnlyList<string> headers)
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

        return new ChoiceColumn(property, CanonicalKey(property, header[(separator + ChoiceSeparator.Length)..]));
    }

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

    private static bool IsTruthy(string cell)
    {
        string trimmed = cell.Trim();
        return trimmed.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("x", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The ticked choice columns of one row: one key per single-choice property, a list per
    /// multi-choice property.
    /// </summary>
    private sealed class ChoiceCells(Dictionary<long, string> values)
    {
        private readonly Dictionary<long, List<string>> _multi = new();
        private readonly HashSet<long> _single = [];
        private string? _error;

        public void Tick(ChoiceColumn column)
        {
            if (column.Property.DataType == AudienceDataTypeCodes.MultipleChoice)
            {
                MultiChoices(column.Property.Id).Add(column.ChoiceKey);
                return;
            }

            if (!_single.Add(column.Property.Id))
            {
                _error ??= $"'{column.Property.Name}' takes one choice, but the row marks more than one.";
            }

            values[column.Property.Id] = column.ChoiceKey;
        }

        public CellRead Finish(JsonSerializerOptions json)
        {
            foreach ((long propertyId, List<string> keys) in _multi)
            {
                values[propertyId] = JsonSerializer.Serialize(keys, json);
            }

            return new CellRead(values, _error);
        }

        private List<string> MultiChoices(long propertyId)
        {
            if (!_multi.TryGetValue(propertyId, out List<string>? keys))
            {
                keys = [];
                _multi[propertyId] = keys;
            }

            return keys;
        }
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
/// A <c>variable__choice</c> header: the choice property it fills and the key a ticked cell sets.
/// </summary>
internal sealed record ChoiceColumn(Property Property, string ChoiceKey);

/// <summary>
/// One row's values keyed by property id, or the reason the row cannot be read.
/// </summary>
internal sealed record CellRead(IReadOnlyDictionary<long, string> Values, string? Error);

internal sealed record CsvColumnMap(
    string IdentifierColumn,
    IReadOnlyDictionary<string, long> SimpleColumns,
    IReadOnlyDictionary<string, ChoiceColumn> ChoiceColumns,
    IReadOnlyDictionary<long, Property> PropertiesById);
