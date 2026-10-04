using Endatix.Core.Common;
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
        string identifierKind)
    {
        RowSorter sorter = new(map, identifierKind);
        foreach (CsvDataRow row in rows)
        {
            sorter.Add(row);
        }

        return new ImportRows(sorter.Accepted, sorter.Rejected);
    }

    private sealed class RowSorter(CsvColumnMap map, string identifierKind)
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public List<ImportRow> Accepted { get; } = [];

        public List<ImportRejectionDto> Rejected { get; } = [];

        public void Add(CsvDataRow row)
        {
            string identifier = Member.Normalize(
                row.Cells.GetValueOrDefault(map.IdentifierColumn) ?? string.Empty, identifierKind);
            string? error = IdentifierError(identifier);
            if (error is not null)
            {
                Rejected.Add(new ImportRejectionDto(row.RowNumber, error));
                return;
            }

            Accepted.Add(new ImportRow(identifier, CsvColumnMapper.ReadValues(map, row.Cells)));
        }

        private string? IdentifierError(string identifier)
        {
            if (identifier.Length == 0)
            {
                return "Identifier is required.";
            }

            if (identifier.Length > DataSchemaConstants.MAX_EMAIL_LENGTH)
            {
                return $"Identifier is longer than {DataSchemaConstants.MAX_EMAIL_LENGTH} characters.";
            }

            return _seen.Add(identifier) ? null : "Identifier appears more than once in the file.";
        }
    }
}

/// <summary>
/// One accepted data row: normalized identifier and property values keyed by property id.
/// </summary>
internal sealed record ImportRow(string Identifier, IReadOnlyDictionary<long, string> Values);

/// <summary>
/// Rows to write, and every row refused with its reason.
/// </summary>
internal sealed record ImportRows(
    IReadOnlyList<ImportRow> Accepted,
    IReadOnlyList<ImportRejectionDto> Rejected);
