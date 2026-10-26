using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Which stored values an import clears for a participant already on the form. Pure: the write applies it per row.
/// </summary>
internal static class ImportClears
{
    /// <summary>
    /// The Properties a row of <paramref name="import"/> clears when it has no value for them. A choice Property with
    /// <c>variable__choice</c> columns is in the file, and a row that ticks none of them leaves it empty. Only the
    /// Properties the form had when the import started, so every batch and a resume clear the same ones whatever was
    /// added since. The match-key Property is never cleared: it holds the identifier.
    /// </summary>
    public static IReadOnlySet<long> Of(CsvColumnMap map, AudienceImport import, MatchKey? matchKey)
    {
        AudienceImportOptions options = import.Options;
        HashSet<long> inFile =
            [.. map.SimpleColumns.Values, .. map.ChoiceColumns.Values.Select(column => column.Property.Id)];
        return map.PropertiesById.Values
            .Where(property => property.CreatedAt <= import.CreatedAt && property.Id != matchKey?.PropertyId)
            .Where(property => inFile.Contains(property.Id) ? options.ClearEmptyCells : options.ClearMissingProperties)
            .Select(property => property.Id)
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
        if (clears.Count == 0)
        {
            return values;
        }

        List<long> cleared =
            [.. storedPropertyIds.Where(propertyId => clears.Contains(propertyId) && !values.ContainsKey(propertyId))];
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
