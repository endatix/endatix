using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Which stored values an import clears for a participant already on the form. Pure: the write applies it per row.
/// </summary>
internal static class ImportClears
{
    /// <summary>
    /// The Properties a row clears when it has no value for them. A choice Property with <c>variable__choice</c>
    /// columns is in the file, and a row that ticks none of them leaves it empty. The match-key Property is never
    /// cleared: it holds the identifier.
    /// </summary>
    public static IReadOnlySet<long> Of(CsvColumnMap map, AudienceImportOptions options, MatchKey? matchKey)
    {
        HashSet<long> inFile = [.. map.SimpleColumns.Values, .. map.ChoiceColumns.Values.Select(column => column.Property.Id)];
        return map.PropertiesById.Keys
            .Where(propertyId => inFile.Contains(propertyId) ? options.ClearEmptyCells : options.ClearMissingProperties)
            .Where(propertyId => propertyId != matchKey?.PropertyId)
            .ToHashSet();
    }

    /// <summary>
    /// The row's values plus an empty value, which clears, for each Property in <paramref name="clears"/> that has a
    /// stored cell and no value in the row. A Property with nothing stored gets none, so clearing nothing changes
    /// nothing.
    /// </summary>
    public static IReadOnlyDictionary<long, string> Apply(
        IReadOnlyDictionary<long, string> values,
        IReadOnlySet<long> clears,
        IReadOnlyCollection<long> storedPropertyIds)
    {
        List<long> cleared = [.. storedPropertyIds.Where(propertyId => clears.Contains(propertyId) && !values.ContainsKey(propertyId))];
        if (cleared.Count == 0)
        {
            return values;
        }

        Dictionary<long, string> written = new(values);
        foreach (long propertyId in cleared)
        {
            written[propertyId] = string.Empty;
        }

        return written;
    }
}
