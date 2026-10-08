using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Resolves CSV headers to property ids, including <c>variable__choice</c> multi-choice columns.
/// </summary>
internal static class CsvColumnMapper
{
    public static CsvColumnMap Build(
        IReadOnlyList<Property> properties,
        string identifierColumn,
        IReadOnlyDictionary<string, string>? propertyColumns)
    {
        Dictionary<string, Property> byVariable = properties.ToDictionary(
            property => property.VariableName,
            StringComparer.OrdinalIgnoreCase);
        return new CsvColumnMap(
            identifierColumn,
            BuildSimpleColumns(byVariable, propertyColumns),
            byVariable);
    }

    /// <summary>
    /// Refuses a mapping that names a property this form lacks or a column the file lacks, so a
    /// typo cannot drop a whole column while the import reports success.
    /// </summary>
    public static string? MappingError(
        IReadOnlyList<Property> properties,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string>? propertyColumns)
    {
        HashSet<string> variables = properties
            .Select(property => property.VariableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> columns = headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return propertyColumns?
            .Select(column => ColumnError(variables, columns, column))
            .FirstOrDefault(error => error is not null);
    }

    public static IReadOnlyDictionary<long, string> ReadValues(
        CsvColumnMap map,
        IReadOnlyDictionary<string, string> row)
    {
        Dictionary<long, string> values = ReadSimpleValues(map, row);
        ApplyChoiceColumns(map, row, values);
        return values;
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

    private static Dictionary<string, long> BuildSimpleColumns(
        IReadOnlyDictionary<string, Property> byVariable,
        IReadOnlyDictionary<string, string>? propertyColumns)
    {
        Dictionary<string, long> simple = new(StringComparer.OrdinalIgnoreCase);
        if (propertyColumns is null)
        {
            return simple;
        }

        foreach ((string variableName, string csvHeader) in propertyColumns)
        {
            if (byVariable.TryGetValue(variableName, out Property? property))
            {
                simple[csvHeader] = property.Id;
            }
        }

        return simple;
    }

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

    private static void ApplyChoiceColumns(
        CsvColumnMap map,
        IReadOnlyDictionary<string, string> row,
        Dictionary<long, string> values)
    {
        Dictionary<long, List<string>> multi = new();
        foreach ((string header, string cell) in row)
        {
            ApplyChoiceHeader(new ChoiceHeaderArgs(map, header, cell, values, multi));
        }

        foreach ((long propertyId, List<string> choices) in multi)
        {
            values[propertyId] = System.Text.Json.JsonSerializer.Serialize(choices);
        }
    }

    private static void ApplyChoiceHeader(ChoiceHeaderArgs args)
    {
        int separator = args.Header.IndexOf("__", StringComparison.Ordinal);
        if (separator <= 0 || !IsTruthy(args.Cell))
        {
            return;
        }

        string variable = args.Header[..separator];
        string choice = args.Header[(separator + 2)..];
        if (!args.Map.PropertiesByVariable.TryGetValue(variable, out Property? property))
        {
            return;
        }

        AddChoice(args, property, choice);
    }

    private static void AddChoice(
        ChoiceHeaderArgs args,
        Property property,
        string choice)
    {
        if (property.DataType == AudienceDataTypeCodes.SingleChoice)
        {
            args.Values[property.Id] = choice;
            return;
        }

        if (property.DataType != AudienceDataTypeCodes.MultipleChoice)
        {
            return;
        }

        AppendMultiChoice(args, property.Id, choice);
    }

    private static void AppendMultiChoice(
        ChoiceHeaderArgs args,
        long propertyId,
        string choice)
    {
        if (!args.Multi.TryGetValue(propertyId, out List<string>? choices))
        {
            choices = [];
            args.Multi[propertyId] = choices;
        }

        choices.Add(choice);
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
}

internal sealed record ChoiceHeaderArgs(
    CsvColumnMap Map,
    string Header,
    string Cell,
    Dictionary<long, string> Values,
    Dictionary<long, List<string>> Multi);

internal sealed record CsvColumnMap(
    string IdentifierColumn,
    IReadOnlyDictionary<string, long> SimpleColumns,
    IReadOnlyDictionary<string, Property> PropertiesByVariable);
