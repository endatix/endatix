using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Turns parsed CSV rows into accepted rows and rejected rows. Pure: touches no database.
/// </summary>
internal static class ImportRowReader
{
    public static ImportRows Read(
        IReadOnlyList<CsvDataRow> rows,
        CsvColumnMap map,
        ImportMatchKey matchKey) =>
        Start(map, matchKey).ReadPart(rows);

    /// <summary>
    /// A reader for a file read in parts: an identifier accepted in one part makes its repeat in a later part a
    /// duplicate, as it would be in one pass.
    /// </summary>
    public static RowSorter Start(CsvColumnMap map, ImportMatchKey matchKey) => new(map, matchKey);

    internal sealed class RowSorter(CsvColumnMap map, ImportMatchKey matchKey)
    {
        private const string MalformedRow =
            "The row has a quote in the wrong place, so its cells cannot be read reliably.";

        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        private readonly MatchKey? _matchKeyProperty =
            MatchKeyProperty.In(map.PropertiesById.Values, matchKey.IdentifierKind);

        /// <summary>Sorts one part of the file; identifiers accepted by earlier parts stay claimed.</summary>
        public ImportRows ReadPart(IEnumerable<CsvDataRow> rows)
        {
            List<ImportRow> accepted = [];
            List<ImportRejectionDto> rejected = [];
            foreach (CsvDataRow row in rows)
            {
                Add(row, accepted, rejected);
            }

            return new ImportRows(accepted, rejected);
        }

        /// <summary>
        /// The repeat check runs last and only an accepted row claims its identifier, so a row
        /// rejected for a bad value does not turn a later, valid row for the same person into a
        /// duplicate. A column mapped to the match-key Property may hold the identifier or nothing, which is not stored.
        /// </summary>
        private void Add(CsvDataRow row, List<ImportRow> accepted, List<ImportRejectionDto> rejected)
        {
            string identifier = (row.Cells.GetValueOrDefault(map.IdentifierColumn) ?? string.Empty).Trim();
            CellRead cells = CsvColumnMapper.ReadValues(map, row.Cells);
            if (ErrorOf(row, identifier, cells) is { } error)
            {
                rejected.Add(new ImportRejectionDto(row.RowNumber, error));
                return;
            }

            accepted.Add(new ImportRow(
                row.RowNumber,
                identifier,
                matchKey.Normalize(identifier),
                MatchKeyProperty.Stored(cells.Values, _matchKeyProperty)));
        }

        private string? ErrorOf(CsvDataRow row, string identifier, CellRead cells) =>
            (row.Malformed ? MalformedRow : null)
            ?? Member.IdentifierError(identifier, matchKey.IdentifierKind)
            ?? cells.Error
            ?? MatchKeyProperty.WriteError(cells.Values, _matchKeyProperty, identifier)
            ?? ValuesError(cells.Values)
            ?? RepeatError(identifier);

        private string? RepeatError(string identifier) =>
            _seen.Add(matchKey.Normalize(identifier)) ? null : "Identifier appears more than once in the file.";

        private string? ValuesError(IReadOnlyDictionary<long, string> values) =>
            values
                .Select(cell => PropertyValuesWriter.ValueError(map.PropertiesById[cell.Key], cell.Value))
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
/// One accepted data row: its spreadsheet row, the identifier as entered, its match key, and property values keyed
/// by property id.
/// </summary>
internal sealed record ImportRow(
    int RowNumber,
    string Identifier,
    string NormalizedIdentifier,
    IReadOnlyDictionary<long, string> Values);

/// <summary>
/// Rows to write, and every row refused with its reason.
/// </summary>
internal sealed record ImportRows(
    IReadOnlyList<ImportRow> Accepted,
    IReadOnlyList<ImportRejectionDto> Rejected);
