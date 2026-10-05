using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Turns parsed CSV rows into accepted rows and rejected rows. Pure: touches no database.
/// </summary>
internal static class ImportRowReader
{
    public static ImportRows Read(
        IReadOnlyList<CsvDataRow> rows,
        CsvColumnMap map,
        ImportMatchKey matchKey)
    {
        RowSorter sorter = new(map, matchKey);
        foreach (CsvDataRow row in rows)
        {
            sorter.Add(row);
        }

        return new ImportRows(sorter.Accepted, sorter.Rejected);
    }

    private sealed class RowSorter(CsvColumnMap map, ImportMatchKey matchKey)
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly Dictionary<long, Property> _propertiesById =
            map.PropertiesByVariable.Values.ToDictionary(property => property.Id);

        public List<ImportRow> Accepted { get; } = [];

        public List<ImportRejectionDto> Rejected { get; } = [];

        public void Add(CsvDataRow row)
        {
            string identifier = (row.Cells.GetValueOrDefault(map.IdentifierColumn) ?? string.Empty).Trim();
            IReadOnlyDictionary<long, string> values = CsvColumnMapper.ReadValues(map, row.Cells);
            string? error = Member.IdentifierError(identifier, matchKey.IdentifierKind)
                ?? RepeatError(identifier)
                ?? ValuesError(values);
            if (error is not null)
            {
                Rejected.Add(new ImportRejectionDto(row.RowNumber, error));
                return;
            }

            Accepted.Add(new ImportRow(identifier, matchKey.Normalize(identifier), values));
        }

        private string? RepeatError(string identifier) =>
            _seen.Add(matchKey.Normalize(identifier)) ? null : "Identifier appears more than once in the file.";

        private string? ValuesError(IReadOnlyDictionary<long, string> values) =>
            values
                .Select(cell => PropertyValue.ValueError(cell.Value) ?? _propertiesById[cell.Key].ValueError(cell.Value))
                .FirstOrDefault(error => error is not null);
    }
}

/// <summary>
/// The tenant match key an import normalizes identifiers with.
/// </summary>
internal sealed record ImportMatchKey(string IdentifierKind, IValueNormalizer Normalizer)
{
    public string Normalize(string identifier) => Member.Normalize(identifier, IdentifierKind, Normalizer);
}

/// <summary>
/// One accepted data row: the identifier as entered, its match key, and property values keyed by
/// property id.
/// </summary>
internal sealed record ImportRow(
    string Identifier,
    string NormalizedIdentifier,
    IReadOnlyDictionary<long, string> Values);

/// <summary>
/// Rows to write, and every row refused with its reason.
/// </summary>
internal sealed record ImportRows(
    IReadOnlyList<ImportRow> Accepted,
    IReadOnlyList<ImportRejectionDto> Rejected);
